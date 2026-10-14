using System.Text.Json;
using Kadr.Infrastructure.Persistence.Employees;
using Kadr.Platform.Application.Attendance;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Attendance;

public sealed partial class AttendanceService
{
    public async Task<Result<PunchResultDto>> ProxyPunchAsync(
        ProxyPunchRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<PunchResultDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<PunchResultDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (request.EmployeeId == Guid.Empty || request.ClientRequestId == Guid.Empty)
        {
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > ProxyPunchRequestValidator.ReasonMaxLength)
        {
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceReasonRequired,
                ErrorCodes.AttendanceReasonRequired);
        }

        var tenantId = tenantContext.TenantId.Value;
        var actorUserId = tenantContext.UserId.Value;

        var prior = await db.AttendancePunches.AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.EmployeeId == request.EmployeeId && p.ClientRequestId == request.ClientRequestId,
                cancellationToken)
            .ConfigureAwait(false);
        if (prior is not null)
        {
            return Result.Success(new PunchResultDto(
                prior.Id,
                prior.Kind.ToString(),
                prior.PunchedAt,
                prior.WorkDate,
                prior.IsInside,
                prior.DistanceM ?? 0,
                prior.OutsideReason,
                (await GetOrComputeStatusAsync(request.EmployeeId, prior.WorkDate, cancellationToken)
                    .ConfigureAwait(false)).ToString(),
                prior.WorkLocationId));
        }

        var scope = await EmployeeDataScope.ResolveAsync(db, actorUserId, cancellationToken)
            .ConfigureAwait(false);
        var inScope = await EmployeeDataScope.Apply(
                db.Employees.AsNoTracking(),
                scope.Scope,
                actorUserId,
                scope.LinkedEmployeeId,
                scope.DepartmentId,
                scope.BranchId)
            .AnyAsync(e => e.Id == request.EmployeeId, cancellationToken)
            .ConfigureAwait(false);
        if (!inScope)
        {
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceNoEmployee,
                ErrorCodes.AttendanceNoEmployee);
        }

        var employee = await db.Employees
            .SingleOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            .ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceNoEmployee,
                ErrorCodes.AttendanceNoEmployee);
        }

        if (employee.Status is not (EmployeeStatus.Active or EmployeeStatus.Probation))
        {
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceEmployeeInactive,
                ErrorCodes.AttendanceEmployeeInactive);
        }

        var tenant = await db.Tenants.AsNoTracking()
            .SingleAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var punchedAt = (request.PunchedAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var context = await ResolvePunchContextAsync(
                employee.Id,
                punchedAt,
                tenant.TimeZone,
                cancellationToken)
            .ConfigureAwait(false);

        var kind = await ResolveNextKindAsync(employee.Id, context.WorkDate, cancellationToken)
            .ConfigureAwait(false);

        var punch = AttendancePunch.CreateAccepted(
            tenantId,
            employee.Id,
            employee.BranchId,
            kind,
            punchedAt,
            context.WorkDate,
            context.ShiftId,
            PunchSource.Manual,
            accuracyM: 0,
            distanceM: 0,
            isInside: true,
            outsideReason: reason,
            latitude: 0,
            longitude: 0,
            request.ClientRequestId,
            actorUserId,
            workLocationId: null,
            photoFileId: null);

        db.AttendancePunches.Add(punch);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            var raced = await db.AttendancePunches.AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.EmployeeId == employee.Id && p.ClientRequestId == request.ClientRequestId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (raced is not null)
            {
                return Result.Success(new PunchResultDto(
                    raced.Id,
                    raced.Kind.ToString(),
                    raced.PunchedAt,
                    raced.WorkDate,
                    raced.IsInside,
                    raced.DistanceM ?? 0,
                    raced.OutsideReason,
                    (await GetOrComputeStatusAsync(employee.Id, raced.WorkDate, cancellationToken)
                        .ConfigureAwait(false)).ToString(),
                    raced.WorkLocationId));
            }

            throw;
        }

        var status = await RecomputeSummaryAsync(
                tenantId,
                employee.Id,
                context.WorkDate,
                tenant.TimeZone,
                cancellationToken)
            .ConfigureAwait(false);

        await audit.WriteAsync(
                tenantId,
                actorUserId,
                "attendance.proxy_punch",
                nameof(AttendancePunch),
                punch.Id,
                null,
                JsonSerializer.Serialize(new
                {
                    actorUserId,
                    employeeId = employee.Id,
                    kind = punch.Kind.ToString(),
                    workDate = punch.WorkDate.ToString("O"),
                    reason,
                    source = PunchSource.Manual.ToString(),
                }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PunchResultDto(
            punch.Id,
            punch.Kind.ToString(),
            punch.PunchedAt,
            punch.WorkDate,
            punch.IsInside,
            punch.DistanceM ?? 0,
            punch.OutsideReason,
            status.ToString(),
            punch.WorkLocationId));
    }
}
