using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

public sealed class EmployeeInsuranceService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IAuditLogWriter audit) : IEmployeeInsuranceService
{
    public async Task<Result<EmployeeInsuranceProfileDto?>> GetAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var employeeOk = await EnsureEmployeeAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (employeeOk.IsFailure)
        {
            return Result.Failure<EmployeeInsuranceProfileDto?>(
                employeeOk.ErrorCode!,
                employeeOk.ErrorMessage!);
        }

        var row = await db.EmployeeInsuranceProfiles.AsNoTracking()
            .Where(p => p.EmployeeId == employeeId)
            .Select(p => new EmployeeInsuranceProfileDto(
                p.Id,
                p.EmployeeId,
                p.IsInsured,
                p.InsuranceNumber,
                p.InsuranceStartDate,
                p.InsurableWageOverride))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(row);
    }

    public async Task<Result<EmployeeInsuranceProfileDto>> UpsertAsync(
        Guid employeeId,
        UpsertEmployeeInsuranceRequest request,
        CancellationToken cancellationToken = default)
    {
        var employeeOk = await EnsureEmployeeAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (employeeOk.IsFailure)
        {
            return Result.Failure<EmployeeInsuranceProfileDto>(
                employeeOk.ErrorCode!,
                employeeOk.ErrorMessage!);
        }

        var entity = await db.EmployeeInsuranceProfiles
            .SingleOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (entity is null)
            {
                entity = EmployeeInsuranceProfile.Create(
                    RequireTenantId(),
                    employeeId,
                    request.IsInsured,
                    request.InsuranceNumber,
                    request.InsuranceStartDate,
                    request.InsurableWageOverride);
                db.EmployeeInsuranceProfiles.Add(entity);
                StageAudit(
                    "employee_insurance.create",
                    entity.Id,
                    $"{{\"employeeId\":\"{employeeId}\",\"isInsured\":{request.IsInsured.ToString().ToLowerInvariant()}}}");
            }
            else
            {
                entity.Update(
                    request.IsInsured,
                    request.InsuranceNumber,
                    request.InsuranceStartDate,
                    request.InsurableWageOverride);
                StageAudit(
                    "employee_insurance.update",
                    entity.Id,
                    $"{{\"employeeId\":\"{employeeId}\",\"isInsured\":{request.IsInsured.ToString().ToLowerInvariant()}}}");
            }
        }
        catch (ArgumentException)
        {
            return Result.Failure<EmployeeInsuranceProfileDto>(
                ErrorCodes.InsuranceValidationFailed,
                ErrorCodes.InsuranceValidationFailed);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(MapDto(entity));
    }

    private static EmployeeInsuranceProfileDto MapDto(EmployeeInsuranceProfile p) =>
        new(
            p.Id,
            p.EmployeeId,
            p.IsInsured,
            p.InsuranceNumber,
            p.InsuranceStartDate,
            p.InsurableWageOverride);

    private async Task<Result> EnsureEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var exists = await db.Employees.AsNoTracking()
            .AnyAsync(e => e.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);
        return exists
            ? Result.Success()
            : Result.Failure(ErrorCodes.InsuranceEmployeeNotFound, ErrorCodes.InsuranceEmployeeNotFound);
    }

    private Guid RequireTenantId()
    {
        if (tenantContext.TenantId is null)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        return tenantContext.TenantId.Value;
    }

    private void StageAudit(string action, Guid? entityId, string newJson)
    {
        if (tenantContext.TenantId is null)
        {
            return;
        }

        audit.Stage(
            tenantContext.TenantId.Value,
            tenantContext.UserId,
            action,
            "employee_insurance",
            entityId,
            null,
            newJson);
    }
}
