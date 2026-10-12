using System.Text.Json;
using Kadr.Platform.Application.Attendance;
using Kadr.Platform.Domain.Attendance;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Attendance;

public sealed partial class AttendanceService
{
    private const int MaxWorkLocationPageSize = 100;
    private const int MaxAssignmentsPerLocation = 1000;

    public async Task<Result<PagedWorkLocationsDto>> ListWorkLocationsAsync(
        string? type,
        bool? active,
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<PagedWorkLocationsDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        WorkLocationType? typeFilter = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!TryParseDefined<WorkLocationType>(type, out var parsed))
            {
                return Result.Failure<PagedWorkLocationsDto>(
                    ErrorCodes.AttendanceValidationFailed,
                    ErrorCodes.AttendanceValidationFailed);
            }

            typeFilter = parsed;
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxWorkLocationPageSize);

        var query = db.WorkLocations.AsNoTracking().AsQueryable();
        if (typeFilter is { } t)
        {
            query = query.Where(l => l.Type == t);
        }

        if (active is { } isActive)
        {
            query = query.Where(l => l.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{EscapeLikePattern(q.Trim())}%";
            query = query.Where(l =>
                EF.Functions.ILike(l.NameAr, pattern)
                || (l.NameEn != null && EF.Functions.ILike(l.NameEn, pattern)));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(l => l.IsActive)
            .ThenBy(l => l.NameAr)
            .ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new WorkLocationDto(
                l.Id,
                l.NameAr,
                l.NameEn,
                l.Type.ToString(),
                l.LinkedBranchId,
                l.Latitude,
                l.Longitude,
                l.GeofenceRadiusMeters,
                l.Address,
                l.ValidFrom,
                l.ValidTo,
                l.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PagedWorkLocationsDto(items, page, pageSize, total));
    }

    public async Task<Result<WorkLocationDto>> GetWorkLocationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<WorkLocationDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var location = await db.WorkLocations.AsNoTracking()
            .SingleOrDefaultAsync(l => l.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return location is null
            ? Result.Failure<WorkLocationDto>(ErrorCodes.AttendanceLocationNotFound, ErrorCodes.AttendanceLocationNotFound)
            : Result.Success(MapWorkLocation(location));
    }

    public async Task<Result<WorkLocationDto>> CreateWorkLocationAsync(
        UpsertWorkLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<WorkLocationDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null)
        {
            return Result.Failure<WorkLocationDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (!TryParseDefined<WorkLocationType>(request.Type, out var type)
            || !await LinkedBranchIsValidAsync(request.LinkedBranchId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<WorkLocationDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        WorkLocation location;
        try
        {
            location = WorkLocation.Create(
                tenantContext.TenantId.Value,
                request.NameAr,
                request.NameEn,
                type,
                request.LinkedBranchId,
                request.Latitude,
                request.Longitude,
                request.GeofenceRadiusMeters,
                request.Address,
                request.ValidFrom,
                request.ValidTo,
                request.IsActive,
                tenantContext.UserId);
        }
        catch (ArgumentException)
        {
            return Result.Failure<WorkLocationDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        db.WorkLocations.Add(location);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                location.TenantId,
                tenantContext.UserId,
                "attendance.work_location.create",
                nameof(WorkLocation),
                location.Id,
                null,
                JsonSerializer.Serialize(new { type = location.Type.ToString(), isActive = location.IsActive }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(MapWorkLocation(location));
    }

    public async Task<Result<WorkLocationDto>> UpdateWorkLocationAsync(
        Guid id,
        UpsertWorkLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<WorkLocationDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var location = await db.WorkLocations.SingleOrDefaultAsync(l => l.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (location is null)
        {
            return Result.Failure<WorkLocationDto>(
                ErrorCodes.AttendanceLocationNotFound,
                ErrorCodes.AttendanceLocationNotFound);
        }

        if (!TryParseDefined<WorkLocationType>(request.Type, out var type)
            || !await LinkedBranchIsValidAsync(request.LinkedBranchId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<WorkLocationDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        var before = JsonSerializer.Serialize(new { type = location.Type.ToString(), isActive = location.IsActive });
        try
        {
            location.Update(
                request.NameAr,
                request.NameEn,
                type,
                request.LinkedBranchId,
                request.Latitude,
                request.Longitude,
                request.GeofenceRadiusMeters,
                request.Address,
                request.ValidFrom,
                request.ValidTo,
                request.IsActive,
                tenantContext.UserId);
        }
        catch (ArgumentException)
        {
            return Result.Failure<WorkLocationDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                location.TenantId,
                tenantContext.UserId,
                "attendance.work_location.update",
                nameof(WorkLocation),
                location.Id,
                before,
                JsonSerializer.Serialize(new { type = location.Type.ToString(), isActive = location.IsActive }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(MapWorkLocation(location));
    }

    public async Task<Result<IReadOnlyList<WorkLocationAssignmentDto>>> GetWorkLocationAssignmentsAsync(
        Guid workLocationId,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WorkLocationAssignmentDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var exists = await db.WorkLocations.AsNoTracking()
            .AnyAsync(l => l.Id == workLocationId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
        {
            return Result.Failure<IReadOnlyList<WorkLocationAssignmentDto>>(
                ErrorCodes.AttendanceLocationNotFound,
                ErrorCodes.AttendanceLocationNotFound);
        }

        var rows = await LoadAssignmentDtosAsync(workLocationId, cancellationToken).ConfigureAwait(false);
        return Result.Success(rows);
    }

    public async Task<Result<IReadOnlyList<WorkLocationAssignmentDto>>> ReplaceWorkLocationAssignmentsAsync(
        Guid workLocationId,
        ReplaceWorkLocationAssignmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WorkLocationAssignmentDto>>(
                writable.ErrorCode!,
                writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null)
        {
            return Result.Failure<IReadOnlyList<WorkLocationAssignmentDto>>(
                ErrorCodes.TenantNotFound,
                ErrorCodes.TenantNotFound);
        }

        var tenantId = tenantContext.TenantId.Value;
        var location = await db.WorkLocations.AsNoTracking()
            .SingleOrDefaultAsync(l => l.Id == workLocationId, cancellationToken)
            .ConfigureAwait(false);
        if (location is null)
        {
            return Result.Failure<IReadOnlyList<WorkLocationAssignmentDto>>(
                ErrorCodes.AttendanceLocationNotFound,
                ErrorCodes.AttendanceLocationNotFound);
        }

        var inputs = request.Assignments;
        if (inputs is null || inputs.Count > MaxAssignmentsPerLocation)
        {
            return Result.Failure<IReadOnlyList<WorkLocationAssignmentDto>>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        if (inputs.Count > 0 && !location.IsActive)
        {
            return Result.Failure<IReadOnlyList<WorkLocationAssignmentDto>>(
                ErrorCodes.AttendanceLocationInactive,
                ErrorCodes.AttendanceLocationInactive);
        }

        var built = new List<WorkLocationAssignment>(inputs.Count);
        var seen = new HashSet<(WorkLocationAssignmentScope, Guid)>();
        foreach (var input in inputs)
        {
            if (!TryParseDefined<WorkLocationAssignmentScope>(input.Scope, out var scope))
            {
                return InvalidAssignments();
            }

            try
            {
                var assignment = WorkLocationAssignment.Create(
                    tenantId,
                    workLocationId,
                    scope,
                    input.EmployeeId,
                    input.DepartmentId,
                    input.BranchId,
                    input.EffectiveFrom,
                    input.EffectiveTo);
                var key = scope switch
                {
                    WorkLocationAssignmentScope.Employee => assignment.EmployeeId!.Value,
                    WorkLocationAssignmentScope.Department => assignment.DepartmentId!.Value,
                    _ => assignment.BranchId!.Value,
                };
                if (!seen.Add((scope, key)))
                {
                    return InvalidAssignments();
                }

                built.Add(assignment);
            }
            catch (ArgumentException)
            {
                return InvalidAssignments();
            }
        }

        if (!await AssignmentTargetsExistAsync(built, cancellationToken).ConfigureAwait(false))
        {
            return InvalidAssignments();
        }

        var existing = await db.WorkLocationAssignments
            .Where(a => a.WorkLocationId == workLocationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.WorkLocationAssignments.RemoveRange(existing);
        db.WorkLocationAssignments.AddRange(built);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                tenantId,
                tenantContext.UserId,
                "attendance.work_location.assignments",
                nameof(WorkLocation),
                workLocationId,
                JsonSerializer.Serialize(new { count = existing.Count }),
                JsonSerializer.Serialize(new { count = built.Count }),
                cancellationToken)
            .ConfigureAwait(false);

        var rows = await LoadAssignmentDtosAsync(workLocationId, cancellationToken).ConfigureAwait(false);
        return Result.Success(rows);

        static Result<IReadOnlyList<WorkLocationAssignmentDto>> InvalidAssignments() =>
            Result.Failure<IReadOnlyList<WorkLocationAssignmentDto>>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
    }

    public async Task<Result<EmployeeAttendancePolicyDto>> GetEmployeeAttendancePolicyAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<EmployeeAttendancePolicyDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var employeeExists = await db.Employees.AsNoTracking()
            .AnyAsync(e => e.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);
        if (!employeeExists)
        {
            return Result.Failure<EmployeeAttendancePolicyDto>(
                ErrorCodes.AttendanceNotFound,
                ErrorCodes.AttendanceNotFound);
        }

        var policy = await db.EmployeeAttendancePolicies.AsNoTracking()
            .SingleOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(policy is null
            ? new EmployeeAttendancePolicyDto(employeeId, false, true, false)
            : MapPolicy(policy));
    }

    public async Task<Result<EmployeeAttendancePolicyDto>> UpdateEmployeeAttendancePolicyAsync(
        Guid employeeId,
        UpdateEmployeeAttendancePolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<EmployeeAttendancePolicyDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null)
        {
            return Result.Failure<EmployeeAttendancePolicyDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var employeeExists = await db.Employees.AsNoTracking()
            .AnyAsync(e => e.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);
        if (!employeeExists)
        {
            return Result.Failure<EmployeeAttendancePolicyDto>(
                ErrorCodes.AttendanceNotFound,
                ErrorCodes.AttendanceNotFound);
        }

        var policy = await db.EmployeeAttendancePolicies
            .SingleOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken)
            .ConfigureAwait(false);
        if (policy is null)
        {
            policy = EmployeeAttendancePolicy.Create(
                tenantContext.TenantId.Value,
                employeeId,
                request.MayClockInFromAnywhere,
                request.AnywhereRequiresReason,
                request.AnywhereRequiresPhoto,
                tenantContext.UserId);
            db.EmployeeAttendancePolicies.Add(policy);
        }
        else
        {
            policy.Update(
                request.MayClockInFromAnywhere,
                request.AnywhereRequiresReason,
                request.AnywhereRequiresPhoto,
                tenantContext.UserId);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                policy.TenantId,
                tenantContext.UserId,
                "attendance.employee_policy.update",
                nameof(EmployeeAttendancePolicy),
                employeeId,
                null,
                JsonSerializer.Serialize(new
                {
                    mayClockInFromAnywhere = policy.MayClockInFromAnywhere,
                    anywhereRequiresReason = policy.AnywhereRequiresReason,
                    anywhereRequiresPhoto = policy.AnywhereRequiresPhoto,
                }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(MapPolicy(policy));
    }

    // --- punch resolution ---

    /// <summary>
    /// Active, valid work locations that apply to the employee on the work date: direct, department and branch scopes.
    /// </summary>
    private async Task<List<PunchGeoTarget>> ResolveAssignedLocationsAsync(
        Guid employeeId,
        Guid? departmentId,
        Guid branchId,
        DateOnly workDate,
        CancellationToken cancellationToken)
    {
        var assignments = db.WorkLocationAssignments.AsNoTracking()
            .Where(a =>
                (a.EffectiveFrom == null || a.EffectiveFrom <= workDate)
                && (a.EffectiveTo == null || a.EffectiveTo >= workDate)
                && ((a.Scope == WorkLocationAssignmentScope.Employee && a.EmployeeId == employeeId)
                    || (a.Scope == WorkLocationAssignmentScope.Department
                        && departmentId != null
                        && a.DepartmentId == departmentId)
                    || (a.Scope == WorkLocationAssignmentScope.Branch && a.BranchId == branchId)));

        var locations = db.WorkLocations.AsNoTracking()
            .Where(l =>
                l.IsActive
                && (l.ValidFrom == null || l.ValidFrom <= workDate)
                && (l.ValidTo == null || l.ValidTo >= workDate));

        return await assignments
            .Join(locations, a => a.WorkLocationId, l => l.Id, (_, l) => new PunchGeoTarget(
                l.Id,
                l.Latitude,
                l.Longitude,
                l.GeofenceRadiusMeters))
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed record PunchGeoTarget(
        Guid? WorkLocationId,
        double Latitude,
        double Longitude,
        int RadiusMeters);

    // --- helpers ---

    private async Task<bool> LinkedBranchIsValidAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        if (branchId is null)
        {
            return true;
        }

        return await db.Branches.AsNoTracking()
            .AnyAsync(b => b.Id == branchId.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> AssignmentTargetsExistAsync(
        IReadOnlyList<WorkLocationAssignment> assignments,
        CancellationToken cancellationToken)
    {
        var employeeIds = assignments
            .Where(a => a.Scope == WorkLocationAssignmentScope.Employee)
            .Select(a => a.EmployeeId!.Value)
            .ToList();
        var departmentIds = assignments
            .Where(a => a.Scope == WorkLocationAssignmentScope.Department)
            .Select(a => a.DepartmentId!.Value)
            .ToList();
        var branchIds = assignments
            .Where(a => a.Scope == WorkLocationAssignmentScope.Branch)
            .Select(a => a.BranchId!.Value)
            .ToList();

        if (employeeIds.Count > 0)
        {
            var found = await db.Employees.AsNoTracking()
                .CountAsync(e => employeeIds.Contains(e.Id), cancellationToken)
                .ConfigureAwait(false);
            if (found != employeeIds.Count)
            {
                return false;
            }
        }

        if (departmentIds.Count > 0)
        {
            var found = await db.Departments.AsNoTracking()
                .CountAsync(d => departmentIds.Contains(d.Id), cancellationToken)
                .ConfigureAwait(false);
            if (found != departmentIds.Count)
            {
                return false;
            }
        }

        if (branchIds.Count > 0)
        {
            var found = await db.Branches.AsNoTracking()
                .CountAsync(b => branchIds.Contains(b.Id), cancellationToken)
                .ConfigureAwait(false);
            if (found != branchIds.Count)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<IReadOnlyList<WorkLocationAssignmentDto>> LoadAssignmentDtosAsync(
        Guid workLocationId,
        CancellationToken cancellationToken) =>
        await db.WorkLocationAssignments.AsNoTracking()
            .Where(a => a.WorkLocationId == workLocationId)
            .OrderBy(a => a.Scope)
            .ThenBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => new WorkLocationAssignmentDto(
                a.Id,
                a.Scope.ToString(),
                a.EmployeeId,
                a.DepartmentId,
                a.BranchId,
                a.EffectiveFrom,
                a.EffectiveTo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static bool TryParseDefined<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value)
               && !char.IsDigit(value.Trim()[0])
               && Enum.TryParse(value.Trim(), true, out result)
               && Enum.IsDefined(result);
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static WorkLocationDto MapWorkLocation(WorkLocation l) =>
        new(
            l.Id,
            l.NameAr,
            l.NameEn,
            l.Type.ToString(),
            l.LinkedBranchId,
            l.Latitude,
            l.Longitude,
            l.GeofenceRadiusMeters,
            l.Address,
            l.ValidFrom,
            l.ValidTo,
            l.IsActive);

    private static EmployeeAttendancePolicyDto MapPolicy(EmployeeAttendancePolicy p) =>
        new(p.EmployeeId, p.MayClockInFromAnywhere, p.AnywhereRequiresReason, p.AnywhereRequiresPhoto);
}
