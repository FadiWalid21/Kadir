using System.Globalization;
using System.Text;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

public sealed class SalaryImportService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IEmployeeSalaryService salaries,
    IAuditLogWriter audit) : ISalaryImportService
{
    private static readonly string[] FixedHeaders =
    [
        "employeeNumber",
        "effectiveFrom",
        "basicAmount",
        "changeReason",
        "currency",
    ];

    public async Task<Result<byte[]>> GetTemplateAsync(CancellationToken cancellationToken = default)
    {
        var componentKeys = await db.PayComponents.AsNoTracking()
            .Where(c => c.IsActive && c.Key != PayComponent.BasicKey)
            .OrderBy(c => c.Key)
            .Select(c => c.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var headers = FixedHeaders.Concat(componentKeys);
        var csv = string.Join(',', headers) + "\n";
        return Result.Success(Encoding.UTF8.GetBytes(csv));
    }

    public async Task<Result<SalaryImportPreviewResultDto>> PreviewAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<SalaryImportPreviewResultDto>(
                ErrorCodes.SalaryImportInvalid,
                ErrorCodes.SalaryImportInvalid);
        }

        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return Result.Failure<SalaryImportPreviewResultDto>(
                ErrorCodes.SalaryImportInvalid,
                ErrorCodes.SalaryImportInvalid);
        }

        if (lines.Length > 501)
        {
            return Result.Failure<SalaryImportPreviewResultDto>(
                ErrorCodes.SalaryImportInvalid,
                ErrorCodes.SalaryImportInvalid);
        }

        var header = SplitCsv(lines[0]).Select(h => h.Trim()).ToArray();
        var headerLower = header.Select(h => h.ToLowerInvariant()).ToArray();

        foreach (var required in new[] { "employeenumber", "effectivefrom", "basicamount", "changereason" })
        {
            if (!headerLower.Contains(required))
            {
                return Result.Failure<SalaryImportPreviewResultDto>(
                    ErrorCodes.SalaryImportInvalid,
                    ErrorCodes.SalaryImportInvalid);
            }
        }

        int Col(string name) => Array.IndexOf(headerLower, name.ToLowerInvariant());

        var employees = await db.Employees.AsNoTracking()
            .Select(e => new { e.Id, e.EmployeeNumber })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var employeesByNumber = employees.ToDictionary(
            e => e.EmployeeNumber,
            e => e.Id,
            StringComparer.OrdinalIgnoreCase);

        var payComponents = await db.PayComponents.AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var payByKey = payComponents.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

        var openByEmployee = await db.EmployeeSalaries.AsNoTracking()
            .Where(s => s.EffectiveTo == null)
            .Select(s => new { s.EmployeeId, s.EffectiveFrom })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var openMap = openByEmployee.ToDictionary(x => x.EmployeeId, x => x.EffectiveFrom);

        var fixedSet = new HashSet<string>(FixedHeaders, StringComparer.OrdinalIgnoreCase);
        var errors = new List<SalaryImportPreviewErrorDto>();
        var valid = new List<SalaryImportCommitRowDto>();
        // Track last effectiveFrom per employee within the file (order matters).
        var pendingOpen = new Dictionary<Guid, DateOnly>(openMap);

        for (var i = 1; i < lines.Length; i++)
        {
            var rowNum = i + 1;
            var cols = SplitCsv(lines[i]);
            string? Cell(string name)
            {
                var idx = Col(name);
                return idx >= 0 && idx < cols.Length ? cols[idx].Trim() : null;
            }

            var employeeNumber = Cell("employeeNumber");
            if (string.IsNullOrWhiteSpace(employeeNumber))
            {
                errors.Add(new SalaryImportPreviewErrorDto(rowNum, "employeeNumber", "required", "employeeNumber is required"));
                continue;
            }

            if (!employeesByNumber.TryGetValue(employeeNumber, out var employeeId))
            {
                errors.Add(new SalaryImportPreviewErrorDto(rowNum, "employeeNumber", "not_found", "Employee not found"));
                continue;
            }

            var fromRaw = Cell("effectiveFrom");
            if (string.IsNullOrWhiteSpace(fromRaw)
                || !DateOnly.TryParse(fromRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var effectiveFrom))
            {
                errors.Add(new SalaryImportPreviewErrorDto(rowNum, "effectiveFrom", "invalid", "Invalid effectiveFrom"));
                continue;
            }

            var basicRaw = Cell("basicAmount");
            if (string.IsNullOrWhiteSpace(basicRaw)
                || !decimal.TryParse(basicRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var basicAmount)
                || basicAmount < 0)
            {
                errors.Add(new SalaryImportPreviewErrorDto(rowNum, "basicAmount", "invalid", "Invalid basicAmount"));
                continue;
            }

            var reason = Cell("changeReason");
            if (string.IsNullOrWhiteSpace(reason))
            {
                errors.Add(new SalaryImportPreviewErrorDto(rowNum, "changeReason", "required", "changeReason is required"));
                continue;
            }

            if (pendingOpen.TryGetValue(employeeId, out var openFrom) && effectiveFrom <= openFrom)
            {
                errors.Add(new SalaryImportPreviewErrorDto(
                    rowNum,
                    "effectiveFrom",
                    "overlap",
                    "effectiveFrom must be after current open salary"));
                continue;
            }

            var currency = Cell("currency");
            if (string.IsNullOrWhiteSpace(currency))
            {
                currency = "EGP";
            }
            else if (currency.Trim().Length != 3)
            {
                errors.Add(new SalaryImportPreviewErrorDto(rowNum, "currency", "invalid", "currency must be 3 letters"));
                continue;
            }

            var components = new List<CreateEmployeeSalaryComponentInput>();
            var componentError = false;
            for (var h = 0; h < header.Length; h++)
            {
                var key = header[h];
                if (fixedSet.Contains(key))
                {
                    continue;
                }

                if (h >= cols.Length || string.IsNullOrWhiteSpace(cols[h]))
                {
                    continue;
                }

                if (string.Equals(key, PayComponent.BasicKey, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new SalaryImportPreviewErrorDto(rowNum, key, "invalid_component", "basic is not a component column"));
                    componentError = true;
                    break;
                }

                if (!payByKey.TryGetValue(key, out var pay) || !pay.IsActive)
                {
                    errors.Add(new SalaryImportPreviewErrorDto(rowNum, key, "invalid_component", "Unknown or inactive pay component"));
                    componentError = true;
                    break;
                }

                if (!decimal.TryParse(cols[h].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                    || amount < 0)
                {
                    errors.Add(new SalaryImportPreviewErrorDto(rowNum, key, "invalid", "Invalid amount"));
                    componentError = true;
                    break;
                }

                components.Add(new CreateEmployeeSalaryComponentInput(pay.Id, amount));
            }

            if (componentError)
            {
                continue;
            }

            if (components.Select(c => c.PayComponentId).Distinct().Count() != components.Count)
            {
                errors.Add(new SalaryImportPreviewErrorDto(rowNum, "components", "duplicate", "Duplicate pay component"));
                continue;
            }

            valid.Add(new SalaryImportCommitRowDto(
                employeeId,
                employeeNumber,
                effectiveFrom,
                currency.Trim().ToUpperInvariant(),
                basicAmount,
                reason.Trim(),
                components));
            pendingOpen[employeeId] = effectiveFrom;
        }

        return Result.Success(new SalaryImportPreviewResultDto(valid.Count, errors.Count, errors, valid));
    }

    public async Task<Result<SalaryImportCommitResultDto>> CommitAsync(
        IReadOnlyList<SalaryImportCommitRowDto> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count > 500)
        {
            return Result.Failure<SalaryImportCommitResultDto>(
                ErrorCodes.SalaryImportInvalid,
                ErrorCodes.SalaryImportInvalid);
        }

        var errors = new List<SalaryImportPreviewErrorDto>();
        var imported = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var result = await salaries.CreateAsync(
                    row.EmployeeId,
                    new CreateEmployeeSalaryRequest(
                        row.EffectiveFrom,
                        row.Currency,
                        row.BasicAmount,
                        row.ChangeReason,
                        row.Components),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure)
            {
                errors.Add(new SalaryImportPreviewErrorDto(
                    i + 1,
                    "row",
                    result.ErrorCode ?? ErrorCodes.SalaryImportInvalid,
                    result.ErrorMessage ?? "failed"));
                continue;
            }

            imported++;
        }

        if (tenantContext.TenantId is not null)
        {
            await audit.WriteAsync(
                    tenantContext.TenantId.Value,
                    tenantContext.UserId,
                    "employee_salary.imported",
                    "employee_salary",
                    null,
                    null,
                    $"{{\"imported\":{imported},\"skipped\":{errors.Count}}}",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success(new SalaryImportCommitResultDto(imported, errors.Count, errors));
    }

    private static string[] SplitCsv(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}
