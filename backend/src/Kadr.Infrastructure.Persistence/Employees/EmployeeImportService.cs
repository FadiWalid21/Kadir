using System.Globalization;
using System.Text;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Employees;
using Kadr.Platform.Domain.Employees;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Employees;

public sealed class EmployeeImportService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ISubscriptionGuard subscriptionGuard,
    IEmployeeService employees,
    IActiveEmployeeCounter employeeCounter,
    IAuditLogWriter audit) : IEmployeeImportService
{
    private static readonly string[] Headers =
    [
        "nameAr", "nameEn", "nationalId", "email", "phone", "gender", "dateOfBirth", "address",
        "department", "jobTitle", "branch", "managerEmployeeNumber", "hireDate", "probationEndDate",
        "contractType", "status",
    ];

    public Task<Result<byte[]>> GetTemplateAsync(CancellationToken cancellationToken = default)
    {
        var csv = string.Join(',', Headers) + "\n";
        return Task.FromResult(Result.Success(Encoding.UTF8.GetBytes(csv)));
    }

    public async Task<Result<ImportPreviewResultDto>> PreviewAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var feature = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.CoreHr, cancellationToken)
            .ConfigureAwait(false);
        if (feature.IsFailure)
        {
            return Result.Failure<ImportPreviewResultDto>(feature.ErrorCode!, feature.ErrorMessage!);
        }

        if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ImportPreviewResultDto>(
                ErrorCodes.EmployeesImportInvalid,
                ErrorCodes.EmployeesImportInvalid);
        }

        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return Result.Failure<ImportPreviewResultDto>(
                ErrorCodes.EmployeesImportInvalid,
                ErrorCodes.EmployeesImportInvalid);
        }

        if (lines.Length > 501)
        {
            return Result.Failure<ImportPreviewResultDto>(
                ErrorCodes.EmployeesImportInvalid,
                ErrorCodes.EmployeesImportInvalid);
        }

        var header = SplitCsv(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var map = Headers.ToDictionary(
            h => h,
            h => Array.IndexOf(header, h.ToLowerInvariant()));

        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.IsActive)
            .Select(d => new { d.Id, d.NameAr, d.NameEn, d.SourceKey })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var jobs = await db.JobTitles.AsNoTracking()
            .Where(j => j.IsActive)
            .Select(j => new { j.Id, j.NameAr, j.NameEn, j.SourceKey })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var branches = await db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new { b.Id, b.NameAr, b.NameEn })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var managers = await db.Employees.AsNoTracking()
            .Select(e => new { e.Id, e.EmployeeNumber })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var errors = new List<ImportPreviewErrorDto>();
        var valid = new List<CreateEmployeeRequest>();

        for (var i = 1; i < lines.Length; i++)
        {
            var rowNum = i + 1;
            var cols = SplitCsv(lines[i]);
            string? Cell(string key)
            {
                var idx = map[key];
                return idx >= 0 && idx < cols.Length ? cols[idx].Trim() : null;
            }

            var nameAr = Cell("nameAr");
            if (string.IsNullOrWhiteSpace(nameAr))
            {
                errors.Add(new ImportPreviewErrorDto(rowNum, "nameAr", "required", "nameAr is required"));
                continue;
            }

            DateOnly hireDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var hireRaw = Cell("hireDate");
            if (!string.IsNullOrWhiteSpace(hireRaw)
                && !DateOnly.TryParse(hireRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out hireDate))
            {
                errors.Add(new ImportPreviewErrorDto(rowNum, "hireDate", "invalid", "Invalid hireDate"));
                continue;
            }

            Guid? departmentId = null;
            var dep = Cell("department");
            if (!string.IsNullOrWhiteSpace(dep))
            {
                var match = departments.FirstOrDefault(d =>
                    d.NameAr.Equals(dep, StringComparison.OrdinalIgnoreCase)
                    || d.NameEn.Equals(dep, StringComparison.OrdinalIgnoreCase)
                    || (d.SourceKey?.Equals(dep, StringComparison.OrdinalIgnoreCase) ?? false));
                if (match is null)
                {
                    errors.Add(new ImportPreviewErrorDto(rowNum, "department", "not_found", "Department not found"));
                    continue;
                }

                departmentId = match.Id;
            }

            Guid? jobTitleId = null;
            var job = Cell("jobTitle");
            if (!string.IsNullOrWhiteSpace(job))
            {
                var match = jobs.FirstOrDefault(j =>
                    j.NameAr.Equals(job, StringComparison.OrdinalIgnoreCase)
                    || j.NameEn.Equals(job, StringComparison.OrdinalIgnoreCase)
                    || (j.SourceKey?.Equals(job, StringComparison.OrdinalIgnoreCase) ?? false));
                if (match is null)
                {
                    errors.Add(new ImportPreviewErrorDto(rowNum, "jobTitle", "not_found", "Job title not found"));
                    continue;
                }

                jobTitleId = match.Id;
            }

            Guid? branchId = branches.FirstOrDefault()?.Id;
            var branch = Cell("branch");
            if (!string.IsNullOrWhiteSpace(branch))
            {
                var match = branches.FirstOrDefault(b =>
                    b.NameAr.Equals(branch, StringComparison.OrdinalIgnoreCase)
                    || b.NameEn.Equals(branch, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    errors.Add(new ImportPreviewErrorDto(rowNum, "branch", "not_found", "Branch not found"));
                    continue;
                }

                branchId = match.Id;
            }

            Guid? managerId = null;
            var managerNumber = Cell("managerEmployeeNumber");
            if (!string.IsNullOrWhiteSpace(managerNumber))
            {
                var match = managers.FirstOrDefault(m =>
                    m.EmployeeNumber.Equals(managerNumber, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    errors.Add(new ImportPreviewErrorDto(rowNum, "managerEmployeeNumber", "not_found", "Manager not found"));
                    continue;
                }

                managerId = match.Id;
            }

            DateOnly? dob = null;
            var dobRaw = Cell("dateOfBirth");
            if (!string.IsNullOrWhiteSpace(dobRaw))
            {
                if (!DateOnly.TryParse(dobRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDob))
                {
                    errors.Add(new ImportPreviewErrorDto(rowNum, "dateOfBirth", "invalid", "Invalid dateOfBirth"));
                    continue;
                }

                dob = parsedDob;
            }

            DateOnly? probation = null;
            var probRaw = Cell("probationEndDate");
            if (!string.IsNullOrWhiteSpace(probRaw)
                && DateOnly.TryParse(probRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedProb))
            {
                probation = parsedProb;
            }

            valid.Add(new CreateEmployeeRequest(
                nameAr!,
                Cell("nameEn"),
                Cell("nationalId"),
                Cell("gender"),
                dob,
                Cell("email"),
                Cell("phone"),
                Cell("address"),
                branchId,
                departmentId,
                jobTitleId,
                managerId,
                hireDate,
                probation,
                Cell("contractType"),
                null,
                null,
                Cell("status"),
                null,
                null));
        }

        return Result.Success(new ImportPreviewResultDto(valid.Count, errors.Count, errors, valid));
    }

    public async Task<Result<ImportCommitResultDto>> CommitAsync(
        IReadOnlyList<CreateEmployeeRequest> rows,
        CancellationToken cancellationToken = default)
    {
        var writable = await subscriptionGuard.EnsureWritableAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<ImportCommitResultDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var feature = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.CoreHr, cancellationToken)
            .ConfigureAwait(false);
        if (feature.IsFailure)
        {
            return Result.Failure<ImportCommitResultDto>(feature.ErrorCode!, feature.ErrorMessage!);
        }

        if (tenantContext.TenantId is null)
        {
            return Result.Failure<ImportCommitResultDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var newActive = rows.Count(r =>
        {
            var s = Enum.TryParse<EmployeeStatus>(r.Status, true, out var st) ? st : EmployeeStatus.Active;
            return EmployeeStatusRules.CountsTowardCapacity(s);
        });
        var current = await employeeCounter.CountAsync(tenantContext.TenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        var cap = await subscriptionGuard.EnsureEmployeeCapacityAsync(current + newActive, cancellationToken)
            .ConfigureAwait(false);
        if (cap.IsFailure)
        {
            return Result.Failure<ImportCommitResultDto>(cap.ErrorCode!, cap.ErrorMessage!);
        }

        var errors = new List<ImportPreviewErrorDto>();
        var imported = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var result = await employees.CreateAsync(rows[i], cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                errors.Add(new ImportPreviewErrorDto(
                    i + 1,
                    "row",
                    result.ErrorCode ?? ErrorCodes.EmployeesImportInvalid,
                    result.ErrorMessage ?? "failed"));
                continue;
            }

            imported++;
        }

        await audit.WriteAsync(
                tenantContext.TenantId.Value,
                tenantContext.UserId,
                "employee.imported",
                "Employee",
                null,
                null,
                System.Text.Json.JsonSerializer.Serialize(new { imported, skipped = errors.Count }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new ImportCommitResultDto(imported, errors.Count, errors));
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
