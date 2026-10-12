using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

public sealed class EmployeeSalaryService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IAuditLogWriter audit) : IEmployeeSalaryService
{
    public async Task<Result<PagedEmployeeSalariesDto>> ListAsync(
        Guid employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var employeeOk = await EnsureEmployeeAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (employeeOk.IsFailure)
        {
            return Result.Failure<PagedEmployeeSalariesDto>(employeeOk.ErrorCode!, employeeOk.ErrorMessage!);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.EmployeeSalaries.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(s => s.EffectiveFrom)
            .ThenByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new EmployeeSalaryListItemDto(
                s.Id,
                s.EmployeeId,
                s.EffectiveFrom,
                s.EffectiveTo,
                s.Currency,
                s.BasicAmount,
                s.BasicAmount + s.Components.Sum(c => c.AmountOrRate),
                s.ChangeReason,
                s.Components.Count))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PagedEmployeeSalariesDto(rows, page, pageSize, total));
    }

    public async Task<Result<EmployeeSalaryDto?>> GetCurrentAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var employeeOk = await EnsureEmployeeAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (employeeOk.IsFailure)
        {
            return Result.Failure<EmployeeSalaryDto?>(employeeOk.ErrorCode!, employeeOk.ErrorMessage!);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var salaryId = await db.EmployeeSalaries.AsNoTracking()
            .Where(s =>
                s.EmployeeId == employeeId
                && s.EffectiveFrom <= today
                && (s.EffectiveTo == null || s.EffectiveTo >= today))
            .OrderByDescending(s => s.EffectiveFrom)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (salaryId is null)
        {
            return Result.Success<EmployeeSalaryDto?>(null);
        }

        var detail = await LoadDetailAsync(employeeId, salaryId.Value, cancellationToken)
            .ConfigureAwait(false);
        return detail.IsFailure
            ? Result.Failure<EmployeeSalaryDto?>(detail.ErrorCode!, detail.ErrorMessage!)
            : Result.Success<EmployeeSalaryDto?>(detail.Value);
    }

    public async Task<Result<EmployeeSalaryDto>> GetAsync(
        Guid employeeId,
        Guid salaryId,
        CancellationToken cancellationToken = default)
    {
        var employeeOk = await EnsureEmployeeAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (employeeOk.IsFailure)
        {
            return Result.Failure<EmployeeSalaryDto>(employeeOk.ErrorCode!, employeeOk.ErrorMessage!);
        }

        return await LoadDetailAsync(employeeId, salaryId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<EmployeeSalaryDto>> CreateAsync(
        Guid employeeId,
        CreateEmployeeSalaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var employeeOk = await EnsureEmployeeAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (employeeOk.IsFailure)
        {
            return Result.Failure<EmployeeSalaryDto>(employeeOk.ErrorCode!, employeeOk.ErrorMessage!);
        }

        var componentInputs = request.Components ?? [];
        var componentIds = componentInputs.Select(c => c.PayComponentId).Distinct().ToList();
        if (componentIds.Count != componentInputs.Count)
        {
            return Result.Failure<EmployeeSalaryDto>(
                ErrorCodes.SalaryInvalidComponent,
                ErrorCodes.SalaryInvalidComponent);
        }

        Dictionary<Guid, PayComponent>? componentsById = null;
        if (componentIds.Count > 0)
        {
            var found = await db.PayComponents.AsNoTracking()
                .Where(c => componentIds.Contains(c.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (found.Count != componentIds.Count
                || found.Any(c => !c.IsActive || c.Key == PayComponent.BasicKey))
            {
                return Result.Failure<EmployeeSalaryDto>(
                    ErrorCodes.SalaryInvalidComponent,
                    ErrorCodes.SalaryInvalidComponent);
            }

            componentsById = found.ToDictionary(c => c.Id);
        }

        var open = await db.EmployeeSalaries
            .Include(s => s.Components)
            .SingleOrDefaultAsync(
                s => s.EmployeeId == employeeId && s.EffectiveTo == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (open is not null)
        {
            if (request.EffectiveFrom <= open.EffectiveFrom)
            {
                return Result.Failure<EmployeeSalaryDto>(
                    ErrorCodes.SalaryEffectiveOverlap,
                    ErrorCodes.SalaryEffectiveOverlap);
            }

            try
            {
                open.Close(request.EffectiveFrom.AddDays(-1));
            }
            catch (ArgumentOutOfRangeException)
            {
                return Result.Failure<EmployeeSalaryDto>(
                    ErrorCodes.SalaryEffectiveOverlap,
                    ErrorCodes.SalaryEffectiveOverlap);
            }
        }

        var tenantId = RequireTenantId();
        EmployeeSalary entity;
        try
        {
            entity = EmployeeSalary.Create(
                tenantId,
                employeeId,
                request.EffectiveFrom,
                string.IsNullOrWhiteSpace(request.Currency) ? "EGP" : request.Currency!,
                request.BasicAmount,
                request.ChangeReason,
                componentInputs.Select(c => (c.PayComponentId, c.AmountOrRate)));
        }
        catch (ArgumentException)
        {
            return Result.Failure<EmployeeSalaryDto>(
                ErrorCodes.SalaryValidationFailed,
                ErrorCodes.SalaryValidationFailed);
        }

        db.EmployeeSalaries.Add(entity);
        // Audit without amounts — only ids and dates.
        StageAudit(
            "employee_salary.create",
            entity.Id,
            null,
            $"{{\"employeeId\":\"{employeeId}\",\"effectiveFrom\":\"{entity.EffectiveFrom:O}\",\"closedPriorId\":{(open is null ? "null" : $"\"{open.Id}\"")}}}");
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var lines = MapComponents(entity, componentsById);
        return Result.Success(new EmployeeSalaryDto(
            entity.Id,
            entity.EmployeeId,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.Currency,
            entity.BasicAmount,
            entity.ChangeReason,
            lines));
    }

    private async Task<Result<EmployeeSalaryDto>> LoadDetailAsync(
        Guid employeeId,
        Guid salaryId,
        CancellationToken cancellationToken)
    {
        var salary = await db.EmployeeSalaries.AsNoTracking()
            .Include(s => s.Components)
            .SingleOrDefaultAsync(s => s.Id == salaryId && s.EmployeeId == employeeId, cancellationToken)
            .ConfigureAwait(false);
        if (salary is null)
        {
            return Result.Failure<EmployeeSalaryDto>(ErrorCodes.SalaryNotFound, ErrorCodes.SalaryNotFound);
        }

        var payIds = salary.Components.Select(c => c.PayComponentId).ToList();
        var payMap = await db.PayComponents.AsNoTracking()
            .Where(c => payIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new EmployeeSalaryDto(
            salary.Id,
            salary.EmployeeId,
            salary.EffectiveFrom,
            salary.EffectiveTo,
            salary.Currency,
            salary.BasicAmount,
            salary.ChangeReason,
            MapComponents(salary, payMap)));
    }

    private static IReadOnlyList<EmployeeSalaryComponentDto> MapComponents(
        EmployeeSalary salary,
        IReadOnlyDictionary<Guid, PayComponent>? payMap)
    {
        payMap ??= new Dictionary<Guid, PayComponent>();
        return salary.Components
            .Select(c =>
            {
                payMap.TryGetValue(c.PayComponentId, out var pay);
                return new EmployeeSalaryComponentDto(
                    c.Id,
                    c.PayComponentId,
                    pay?.Key ?? string.Empty,
                    pay?.NameAr ?? string.Empty,
                    pay?.NameEn ?? string.Empty,
                    c.AmountOrRate,
                    pay?.IsTaxable ?? false,
                    pay?.IsInsurable ?? false);
            })
            .ToList();
    }

    private async Task<Result> EnsureEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var exists = await db.Employees.AsNoTracking()
            .AnyAsync(e => e.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);
        return exists
            ? Result.Success()
            : Result.Failure(ErrorCodes.SalaryEmployeeNotFound, ErrorCodes.SalaryEmployeeNotFound);
    }

    private Guid RequireTenantId()
    {
        if (tenantContext.TenantId is null)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        return tenantContext.TenantId.Value;
    }

    private void StageAudit(string action, Guid? entityId, string? oldJson, string? newJson)
    {
        if (tenantContext.TenantId is null)
        {
            return;
        }

        audit.Stage(
            tenantContext.TenantId.Value,
            tenantContext.UserId,
            action,
            "employee_salary",
            entityId,
            oldJson,
            newJson);
    }
}
