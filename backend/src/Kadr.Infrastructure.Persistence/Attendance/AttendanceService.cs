using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Attendance;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Attendance.Geo;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Org;
using Kadr.Platform.Domain.Settings;
using Kadr.Platform.Domain.Templates;
using Kadr.Platform.Domain.Tenants;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Attendance;

public sealed class AttendanceService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ISubscriptionGuard subscriptionGuard,
    IAuditLogWriter audit) : IAttendanceService
{
    private const long MaxPositionAgeMs = 60_000;
    private static readonly string[] ValidWeekendDays = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<PunchResultDto>> PunchAsync(
        PunchRequest request,
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

        if (request.ClientRequestId == Guid.Empty)
        {
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        var tenantId = tenantContext.TenantId.Value;
        var userId = tenantContext.UserId.Value;

        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user?.EmployeeId is null)
        {
            return Result.Failure<PunchResultDto>(ErrorCodes.AttendanceNoEmployee, ErrorCodes.AttendanceNoEmployee);
        }

        var employeeId = user.EmployeeId.Value;
        var prior = await db.AttendancePunches.AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.EmployeeId == employeeId && p.ClientRequestId == request.ClientRequestId,
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
                (await GetOrComputeStatusAsync(employeeId, prior.WorkDate, cancellationToken).ConfigureAwait(false))
                    .ToString()));
        }

        var employee = await db.Employees
            .SingleOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            .ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<PunchResultDto>(ErrorCodes.AttendanceNoEmployee, ErrorCodes.AttendanceNoEmployee);
        }

        if (employee.Status is not (EmployeeStatus.Active or EmployeeStatus.Probation))
        {
            await RecordAttemptAsync(
                    tenantId,
                    employeeId,
                    employee.BranchId,
                    ErrorCodes.AttendanceEmployeeInactive,
                    request.AccuracyM,
                    null,
                    request.ClientRequestId,
                    userId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceEmployeeInactive,
                ErrorCodes.AttendanceEmployeeInactive);
        }

        if (request.PositionAgeMs > MaxPositionAgeMs)
        {
            await RecordAttemptAsync(
                    tenantId,
                    employeeId,
                    employee.BranchId,
                    ErrorCodes.AttendanceLocationStale,
                    request.AccuracyM,
                    null,
                    request.ClientRequestId,
                    userId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceLocationStale,
                ErrorCodes.AttendanceLocationStale);
        }

        var rules = await LoadRulesAsync(cancellationToken).ConfigureAwait(false);
        if (request.AccuracyM > rules.LocationAccuracyM)
        {
            await RecordAttemptAsync(
                    tenantId,
                    employeeId,
                    employee.BranchId,
                    ErrorCodes.AttendanceLocationInaccurate,
                    request.AccuracyM,
                    null,
                    request.ClientRequestId,
                    userId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceLocationInaccurate,
                ErrorCodes.AttendanceLocationInaccurate);
        }

        var branch = await db.Branches
            .SingleOrDefaultAsync(b => b.Id == employee.BranchId, cancellationToken)
            .ConfigureAwait(false);
        if (branch is null || !branch.HasLocationConfigured)
        {
            await RecordAttemptAsync(
                    tenantId,
                    employeeId,
                    employee.BranchId,
                    ErrorCodes.AttendanceBranchNotConfigured,
                    request.AccuracyM,
                    null,
                    request.ClientRequestId,
                    userId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Result.Failure<PunchResultDto>(
                ErrorCodes.AttendanceBranchNotConfigured,
                ErrorCodes.AttendanceBranchNotConfigured);
        }

        var distance = Haversine.DistanceMeters(
            branch.Latitude!.Value,
            branch.Longitude!.Value,
            request.Latitude,
            request.Longitude);
        var isInside = distance <= branch.GeofenceRadiusMeters!.Value;
        string? outsideReason = null;

        if (!isInside)
        {
            if (branch.OutOfRangePolicy == OutOfRangePolicy.Block)
            {
                await RecordAttemptAsync(
                        tenantId,
                        employeeId,
                        branch.Id,
                        ErrorCodes.AttendanceOutsideRange,
                        request.AccuracyM,
                        distance,
                        request.ClientRequestId,
                        userId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return Result.Failure<PunchResultDto>(
                    ErrorCodes.AttendanceOutsideRange,
                    ErrorCodes.AttendanceOutsideRange);
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                await RecordAttemptAsync(
                        tenantId,
                        employeeId,
                        branch.Id,
                        ErrorCodes.AttendanceReasonRequired,
                        request.AccuracyM,
                        distance,
                        request.ClientRequestId,
                        userId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return Result.Failure<PunchResultDto>(
                    ErrorCodes.AttendanceReasonRequired,
                    ErrorCodes.AttendanceReasonRequired);
            }

            outsideReason = request.Reason.Trim();
        }

        var tenant = await db.Tenants.AsNoTracking()
            .SingleAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        var punchedAt = DateTimeOffset.UtcNow;
        var context = await ResolvePunchContextAsync(
                employeeId,
                punchedAt,
                tenant.TimeZone,
                cancellationToken)
            .ConfigureAwait(false);

        var kind = await ResolveNextKindAsync(employeeId, context.WorkDate, cancellationToken)
            .ConfigureAwait(false);

        var punch = AttendancePunch.CreateAccepted(
            tenantId,
            employeeId,
            branch.Id,
            kind,
            punchedAt,
            context.WorkDate,
            context.ShiftId,
            PunchSource.Web,
            request.AccuracyM,
            distance,
            isInside,
            outsideReason,
            request.Latitude,
            request.Longitude,
            request.ClientRequestId,
            userId);

        db.AttendancePunches.Add(punch);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            var raced = await db.AttendancePunches.AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.EmployeeId == employeeId && p.ClientRequestId == request.ClientRequestId,
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
                    (await GetOrComputeStatusAsync(employeeId, raced.WorkDate, cancellationToken)
                        .ConfigureAwait(false)).ToString()));
            }

            throw;
        }

        var status = await RecomputeSummaryAsync(
                tenantId,
                employeeId,
                context.WorkDate,
                tenant.TimeZone,
                cancellationToken)
            .ConfigureAwait(false);

        await audit.WriteAsync(
                tenantId,
                userId,
                "attendance.punch",
                nameof(AttendancePunch),
                punch.Id,
                null,
                JsonSerializer.Serialize(new
                {
                    kind = punch.Kind.ToString(),
                    workDate = punch.WorkDate.ToString("O"),
                    isInside = punch.IsInside,
                    distanceM = punch.DistanceM,
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
            status.ToString()));
    }

    public async Task<Result<AttendanceTodayDto>> GetMyTodayAsync(CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<AttendanceTodayDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var linked = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (linked.IsFailure)
        {
            return Result.Failure<AttendanceTodayDto>(linked.ErrorCode!, linked.ErrorMessage!);
        }

        var (employee, branch, tenant) = linked.Value!;
        var now = DateTimeOffset.UtcNow;
        var context = await ResolvePunchContextAsync(employee.Id, now, tenant.TimeZone, cancellationToken)
            .ConfigureAwait(false);

        var dayFlags = await ResolveDayFlagsAsync(
                employee.Id,
                context.WorkDate,
                tenant.TimeZone,
                cancellationToken)
            .ConfigureAwait(false);

        var punches = await db.AttendancePunches.AsNoTracking()
            .Where(p => p.EmployeeId == employee.Id && p.WorkDate == context.WorkDate)
            .OrderBy(p => p.PunchedAt)
            .Select(p => new AttendancePunchDto(
                p.Id,
                p.Kind.ToString(),
                p.PunchedAt,
                p.IsInside,
                p.DistanceM,
                p.OutsideReason))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var open = punches.LastOrDefault(p => p.Kind == PunchKind.In.ToString());
        if (open is not null && punches.Any(p => p.Kind == PunchKind.Out.ToString() && p.PunchedAt > open.PunchedAt))
        {
            open = null;
        }

        AttendanceShiftDto? shiftDto = null;
        if (context.ShiftId is Guid sid)
        {
            var shift = await db.Shifts.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == sid, cancellationToken)
                .ConfigureAwait(false);
            if (shift is not null)
            {
                shiftDto = new AttendanceShiftDto(
                    shift.Id,
                    shift.NameAr,
                    shift.NameEn,
                    shift.StartTime,
                    shift.EndTime,
                    shift.CrossesMidnight);
            }
        }

        var summary = await EnsureSummaryAsync(
                tenant.Id,
                employee.Id,
                context.WorkDate,
                tenant.TimeZone,
                cancellationToken)
            .ConfigureAwait(false);

        var dayKind = dayFlags.IsHoliday ? "Holiday"
            : dayFlags.IsWeekend || dayFlags.IsDayOff ? "Weekend"
            : context.ShiftId is null ? "NoShift"
            : "Workday";

        var canPunch = employee.Status is EmployeeStatus.Active or EmployeeStatus.Probation
                       && branch.HasLocationConfigured
                       && dayKind == "Workday";

        return Result.Success(new AttendanceTodayDto(
            context.WorkDate,
            dayKind,
            shiftDto,
            MapBranchGeo(branch),
            open,
            punches,
            summary.Status.ToString(),
            summary.LateMinutes,
            canPunch));
    }

    public async Task<Result<AttendanceMonthDto>> GetMyMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<AttendanceMonthDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (year < 2000 || year > 2100 || month is < 1 or > 12)
        {
            return Result.Failure<AttendanceMonthDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        var linked = await ResolveLinkedEmployeeAsync(cancellationToken).ConfigureAwait(false);
        if (linked.IsFailure)
        {
            return Result.Failure<AttendanceMonthDto>(linked.ErrorCode!, linked.ErrorMessage!);
        }

        var (employee, _, tenant) = linked.Value!;
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var today = TenantTimeZones.ToWorkDate(DateTimeOffset.UtcNow, tenant.TimeZone);
        if (end > today)
        {
            end = today;
        }

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            await EnsureSummaryAsync(tenant.Id, employee.Id, d, tenant.TimeZone, cancellationToken)
                .ConfigureAwait(false);
        }

        var rows = await db.AttendanceDaySummaries.AsNoTracking()
            .Where(s => s.EmployeeId == employee.Id && s.WorkDate >= start && s.WorkDate <= end)
            .OrderBy(s => s.WorkDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var days = rows.Select(s => new AttendanceMonthDayDto(
            s.WorkDate,
            s.Status.ToString(),
            s.WorkedMinutes,
            s.LateMinutes,
            s.EarlyLeaveMinutes,
            s.OvertimeMinutes,
            s.FirstInAt,
            s.LastOutAt)).ToList();

        return Result.Success(new AttendanceMonthDto(
            year,
            month,
            days,
            days.Count(d => d.Status == nameof(AttendanceDayStatus.Present)),
            days.Count(d => d.Status == nameof(AttendanceDayStatus.Late)),
            days.Count(d => d.Status == nameof(AttendanceDayStatus.Absent)),
            days.Count(d => d.Status == nameof(AttendanceDayStatus.Incomplete)),
            days.Sum(d => d.WorkedMinutes),
            days.Sum(d => d.LateMinutes),
            days.Sum(d => d.OvertimeMinutes)));
    }

    public async Task<Result<AttendanceRulesDto>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<AttendanceRulesDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        return Result.Success(await LoadRulesAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AttendanceRulesDto>> UpdateRulesAsync(
        UpdateAttendanceRulesRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<AttendanceRulesDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (request.GraceLateMinutes < 0
            || request.GraceEarlyLeaveMinutes < 0
            || request.AbsentAfterStartHours < 0
            || request.OvertimeAfterEndMinutes < 0
            || request.OvertimeMinMinutes < 0
            || request.LocationAccuracyM is < 10 or > 500)
        {
            return Result.Failure<AttendanceRulesDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        await UpsertSettingAsync(SettingKeys.AttendanceGraceLateMinutes, request.GraceLateMinutes, cancellationToken)
            .ConfigureAwait(false);
        await UpsertSettingAsync(
                SettingKeys.AttendanceGraceEarlyLeaveMinutes,
                request.GraceEarlyLeaveMinutes,
                cancellationToken)
            .ConfigureAwait(false);
        await UpsertSettingAsync(
                SettingKeys.AttendanceAbsentAfterStartHours,
                request.AbsentAfterStartHours,
                cancellationToken)
            .ConfigureAwait(false);
        await UpsertSettingAsync(SettingKeys.AttendanceOvertimeEnabled, request.OvertimeEnabled, cancellationToken)
            .ConfigureAwait(false);
        await UpsertSettingAsync(
                SettingKeys.AttendanceOvertimeAfterEndMinutes,
                request.OvertimeAfterEndMinutes,
                cancellationToken)
            .ConfigureAwait(false);
        await UpsertSettingAsync(
                SettingKeys.AttendanceOvertimeMinMinutes,
                request.OvertimeMinMinutes,
                cancellationToken)
            .ConfigureAwait(false);
        await UpsertSettingAsync(SettingKeys.AttendanceLocationAccuracyM, request.LocationAccuracyM, cancellationToken)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(await LoadRulesAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<AttendanceDefaultShiftDto>> GetDefaultShiftAsync(
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<AttendanceDefaultShiftDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var shiftId = await ReadGuidSettingAsync(SettingKeys.AttendanceDefaultShiftId, cancellationToken)
            .ConfigureAwait(false);
        if (shiftId is null)
        {
            return Result.Success(new AttendanceDefaultShiftDto(null, null, null));
        }

        var shift = await db.Shifts.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(new AttendanceDefaultShiftDto(shift?.Id, shift?.NameAr, shift?.NameEn));
    }

    public async Task<Result<AttendanceDefaultShiftDto>> SetDefaultShiftAsync(
        SetDefaultShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<AttendanceDefaultShiftDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        Shift? shift = null;
        if (request.ShiftId is Guid id)
        {
            shift = await db.Shifts.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == id && s.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (shift is null)
            {
                return Result.Failure<AttendanceDefaultShiftDto>(
                    ErrorCodes.AttendanceNotFound,
                    ErrorCodes.AttendanceNotFound);
            }
        }

        await UpsertSettingAsync(
                SettingKeys.AttendanceDefaultShiftId,
                request.ShiftId?.ToString("D"),
                cancellationToken)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new AttendanceDefaultShiftDto(shift?.Id, shift?.NameAr, shift?.NameEn));
    }

    public async Task<Result<IReadOnlyList<BranchLocationDto>>> ListBranchLocationsAsync(
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<BranchLocationDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var rows = await db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.Name)
            .Select(b => new BranchLocationDto(
                b.Id,
                b.Name,
                b.Latitude,
                b.Longitude,
                b.GeofenceRadiusMeters,
                b.OutOfRangePolicy.ToString(),
                b.Latitude != null && b.Longitude != null && b.GeofenceRadiusMeters > 0))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<BranchLocationDto>>(rows);
    }

    public async Task<Result<BranchLocationDto>> UpdateBranchLocationAsync(
        Guid branchId,
        UpdateBranchLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<BranchLocationDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (!Enum.TryParse<OutOfRangePolicy>(request.OutOfRangePolicy, true, out var policy))
        {
            return Result.Failure<BranchLocationDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        var branch = await db.Branches.SingleOrDefaultAsync(b => b.Id == branchId, cancellationToken)
            .ConfigureAwait(false);
        if (branch is null)
        {
            return Result.Failure<BranchLocationDto>(ErrorCodes.AttendanceNotFound, ErrorCodes.AttendanceNotFound);
        }

        try
        {
            branch.SetLocation(request.Latitude, request.Longitude, request.GeofenceRadiusMeters, policy);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result.Failure<BranchLocationDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new BranchLocationDto(
            branch.Id,
            branch.Name,
            branch.Latitude,
            branch.Longitude,
            branch.GeofenceRadiusMeters,
            branch.OutOfRangePolicy.ToString(),
            branch.HasLocationConfigured));
    }

    public async Task<Result<IReadOnlyList<HolidayDto>>> ListHolidaysAsync(
        int? year,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<HolidayDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var query = db.Holidays.AsNoTracking().AsQueryable();
        if (year is int y)
        {
            var from = new DateOnly(y, 1, 1);
            var to = new DateOnly(y, 12, 31);
            query = query.Where(h =>
                h.RepeatsYearly
                || (h.StartsOn <= to && h.EndsOn >= from));
        }

        var rows = await query
            .OrderBy(h => h.StartsOn)
            .Select(h => new HolidayDto(
                h.Id,
                h.StartsOn,
                h.EndsOn,
                h.NameAr,
                h.NameEn,
                h.IsOfficial,
                h.RepeatsYearly,
                h.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<HolidayDto>>(rows);
    }

    public async Task<Result<HolidayDto>> CreateHolidayAsync(
        UpsertHolidayRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<HolidayDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null)
        {
            return Result.Failure<HolidayDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        try
        {
            var holiday = Holiday.Create(
                tenantContext.TenantId.Value,
                request.StartsOn,
                request.EndsOn,
                request.NameAr,
                request.NameEn,
                request.IsOfficial,
                request.RepeatsYearly);
            if (!request.IsActive)
            {
                holiday.Update(
                    request.StartsOn,
                    request.EndsOn,
                    request.NameAr,
                    request.NameEn,
                    request.IsOfficial,
                    request.RepeatsYearly,
                    false);
            }

            db.Holidays.Add(holiday);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(MapHoliday(holiday));
        }
        catch (ArgumentException)
        {
            return Result.Failure<HolidayDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }
    }

    public async Task<Result<HolidayDto>> UpdateHolidayAsync(
        Guid id,
        UpsertHolidayRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<HolidayDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var holiday = await db.Holidays.SingleOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (holiday is null)
        {
            return Result.Failure<HolidayDto>(ErrorCodes.AttendanceNotFound, ErrorCodes.AttendanceNotFound);
        }

        try
        {
            holiday.Update(
                request.StartsOn,
                request.EndsOn,
                request.NameAr,
                request.NameEn,
                request.IsOfficial,
                request.RepeatsYearly,
                request.IsActive);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(MapHoliday(holiday));
        }
        catch (ArgumentException)
        {
            return Result.Failure<HolidayDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }
    }

    public async Task<Result> DeleteHolidayAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return writable;
        }

        var holiday = await db.Holidays.SingleOrDefaultAsync(h => h.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (holiday is null)
        {
            return Result.Failure(ErrorCodes.AttendanceNotFound, ErrorCodes.AttendanceNotFound);
        }

        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<WeekendSettingsDto>> GetWeekendAsync(CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<WeekendSettingsDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var days = await ReadStringArraySettingAsync(SettingKeys.WorkWeekWeekend, ["fri", "sat"], cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(new WeekendSettingsDto(days));
    }

    public async Task<Result<WeekendSettingsDto>> UpdateWeekendAsync(
        UpdateWeekendRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<WeekendSettingsDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var days = (request.WeekendDays ?? [])
            .Select(d => d.Trim().ToLowerInvariant())
            .Where(d => ValidWeekendDays.Contains(d))
            .Distinct()
            .ToArray();
        if (days.Length == 0)
        {
            return Result.Failure<WeekendSettingsDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        await UpsertSettingAsync(SettingKeys.WorkWeekWeekend, days, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new WeekendSettingsDto(days));
    }

    // --- helpers ---

    private async Task RecordAttemptAsync(
        Guid tenantId,
        Guid employeeId,
        Guid? branchId,
        string reasonCode,
        double? accuracyM,
        double? distanceM,
        Guid? clientRequestId,
        Guid? createdBy,
        CancellationToken cancellationToken)
    {
        db.AttendancePunchAttempts.Add(AttendancePunchAttempt.Create(
            tenantId,
            employeeId,
            branchId,
            reasonCode,
            accuracyM,
            distanceM,
            clientRequestId,
            createdBy));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<(Employee Employee, Branch Branch, Tenant Tenant)>>
        ResolveLinkedEmployeeAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<(Employee, Branch, Tenant)>(
                ErrorCodes.TenantNotFound,
                ErrorCodes.TenantNotFound);
        }

        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == tenantContext.UserId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (user?.EmployeeId is null)
        {
            return Result.Failure<(Employee, Branch, Tenant)>(
                ErrorCodes.AttendanceNoEmployee,
                ErrorCodes.AttendanceNoEmployee);
        }

        var employee = await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == user.EmployeeId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (employee is null)
        {
            return Result.Failure<(Employee, Branch, Tenant)>(
                ErrorCodes.AttendanceNoEmployee,
                ErrorCodes.AttendanceNoEmployee);
        }

        var branch = await db.Branches.AsNoTracking()
            .SingleAsync(b => b.Id == employee.BranchId, cancellationToken)
            .ConfigureAwait(false);
        var tenant = await db.Tenants.AsNoTracking()
            .SingleAsync(t => t.Id == tenantContext.TenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success((employee, branch, tenant));
    }

    private async Task<(DateOnly WorkDate, Guid? ShiftId, bool IsDayOff)> ResolvePunchContextAsync(
        Guid employeeId,
        DateTimeOffset punchedAtUtc,
        string? timeZoneId,
        CancellationToken cancellationToken)
    {
        var localDate = TenantTimeZones.ToWorkDate(punchedAtUtc, timeZoneId);
        var candidateDates = new[] { localDate.AddDays(-1), localDate, localDate.AddDays(1) };

        var assignments = await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && candidateDates.Contains(a.WorkDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var defaultShiftId = await ReadGuidSettingAsync(SettingKeys.AttendanceDefaultShiftId, cancellationToken)
            .ConfigureAwait(false);
        Shift? defaultShift = null;
        if (defaultShiftId is Guid dsid)
        {
            defaultShift = await db.Shifts.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == dsid && s.IsActive, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var workDate in candidateDates)
        {
            var assignment = assignments.FirstOrDefault(a => a.WorkDate == workDate);
            if (assignment is { IsDayOff: true })
            {
                if (workDate == localDate)
                {
                    return (workDate, null, true);
                }

                continue;
            }

            Guid? shiftId = assignment?.ShiftId ?? (workDate == localDate ? defaultShiftId : null);
            if (shiftId is null && workDate != localDate)
            {
                continue;
            }

            Shift? shift = null;
            if (shiftId is Guid sid)
            {
                shift = await db.Shifts.AsNoTracking()
                    .SingleOrDefaultAsync(s => s.Id == sid, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (workDate == localDate)
            {
                shift = defaultShift;
                shiftId = defaultShift?.Id;
            }

            if (shift is null)
            {
                if (workDate == localDate)
                {
                    return (localDate, null, false);
                }

                continue;
            }

            var window = AttendanceRulesEngine.BuildWindow(
                shift.Id,
                workDate,
                shift.StartTime,
                shift.EndTime,
                shift.CrossesMidnight,
                shift.BreakMinutes,
                timeZoneId);
            if (AttendanceRulesEngine.Contains(window, punchedAtUtc))
            {
                return (workDate, shift.Id, false);
            }
        }

        return (localDate, defaultShift?.Id, false);
    }

    private async Task<PunchKind> ResolveNextKindAsync(
        Guid employeeId,
        DateOnly workDate,
        CancellationToken cancellationToken)
    {
        var punches = await db.AttendancePunches.AsNoTracking()
            .Where(p => p.EmployeeId == employeeId && p.WorkDate == workDate)
            .OrderBy(p => p.PunchedAt)
            .Select(p => p.Kind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var open = false;
        foreach (var kind in punches)
        {
            if (kind == PunchKind.In)
            {
                open = true;
            }
            else if (kind == PunchKind.Out)
            {
                open = false;
            }
        }

        return open ? PunchKind.Out : PunchKind.In;
    }

    private async Task<AttendanceDayStatus> RecomputeSummaryAsync(
        Guid tenantId,
        Guid employeeId,
        DateOnly workDate,
        string? timeZoneId,
        CancellationToken cancellationToken)
    {
        var summary = await EnsureSummaryAsync(tenantId, employeeId, workDate, timeZoneId, cancellationToken)
            .ConfigureAwait(false);
        return summary.Status;
    }

    private async Task<AttendanceDaySummary> EnsureSummaryAsync(
        Guid tenantId,
        Guid employeeId,
        DateOnly workDate,
        string? timeZoneId,
        CancellationToken cancellationToken)
    {
        var summary = await db.AttendanceDaySummaries
            .SingleOrDefaultAsync(s => s.EmployeeId == employeeId && s.WorkDate == workDate, cancellationToken)
            .ConfigureAwait(false);

        if (summary is { IsManualOverride: true })
        {
            return summary;
        }

        var flags = await ResolveDayFlagsAsync(employeeId, workDate, timeZoneId, cancellationToken)
            .ConfigureAwait(false);
        var punches = await db.AttendancePunches.AsNoTracking()
            .Where(p => p.EmployeeId == employeeId && p.WorkDate == workDate)
            .OrderBy(p => p.PunchedAt)
            .Select(p => new PunchFact(p.Kind, p.PunchedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        ShiftWindow? window = null;
        Guid? shiftId = flags.ShiftId;
        if (shiftId is Guid sid)
        {
            var shift = await db.Shifts.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == sid, cancellationToken)
                .ConfigureAwait(false);
            if (shift is not null)
            {
                window = AttendanceRulesEngine.BuildWindow(
                    shift.Id,
                    workDate,
                    shift.StartTime,
                    shift.EndTime,
                    shift.CrossesMidnight,
                    shift.BreakMinutes,
                    timeZoneId);
            }
            else
            {
                shiftId = null;
            }
        }

        var ruleSettings = await LoadRuleSettingsAsync(cancellationToken).ConfigureAwait(false);
        var computed = AttendanceRulesEngine.Compute(
            punches,
            window,
            ruleSettings,
            flags.IsWeekend,
            flags.IsHoliday,
            flags.IsDayOff,
            shiftId is null && !flags.IsDayOff);

        if (summary is null)
        {
            summary = AttendanceDaySummary.Create(tenantId, employeeId, workDate, shiftId, computed.Status);
            db.AttendanceDaySummaries.Add(summary);
        }

        summary.ApplyComputed(
            shiftId,
            computed.FirstInAt,
            computed.LastOutAt,
            computed.WorkedMinutes,
            computed.LateMinutes,
            computed.EarlyLeaveMinutes,
            computed.OvertimeMinutes,
            computed.Status);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return summary;
    }

    private async Task<AttendanceDayStatus> GetOrComputeStatusAsync(
        Guid employeeId,
        DateOnly workDate,
        CancellationToken cancellationToken)
    {
        var existing = await db.AttendanceDaySummaries.AsNoTracking()
            .SingleOrDefaultAsync(s => s.EmployeeId == employeeId && s.WorkDate == workDate, cancellationToken)
            .ConfigureAwait(false);
        return existing?.Status ?? AttendanceDayStatus.Incomplete;
    }

    private async Task<(
        bool IsWeekend,
        bool IsHoliday,
        bool IsDayOff,
        Guid? ShiftId)> ResolveDayFlagsAsync(
        Guid employeeId,
        DateOnly workDate,
        string? timeZoneId,
        CancellationToken cancellationToken)
    {
        _ = timeZoneId;
        var weekend = await ReadStringArraySettingAsync(SettingKeys.WorkWeekWeekend, ["fri", "sat"], cancellationToken)
            .ConfigureAwait(false);
        var dow = workDate.DayOfWeek switch
        {
            DayOfWeek.Sunday => "sun",
            DayOfWeek.Monday => "mon",
            DayOfWeek.Tuesday => "tue",
            DayOfWeek.Wednesday => "wed",
            DayOfWeek.Thursday => "thu",
            DayOfWeek.Friday => "fri",
            _ => "sat",
        };
        var isWeekend = weekend.Contains(dow, StringComparer.OrdinalIgnoreCase);

        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var isHoliday = holidays.Any(h => h.Covers(workDate));

        var assignment = await db.ShiftAssignments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == workDate, cancellationToken)
            .ConfigureAwait(false);

        Guid? shiftId = assignment?.IsDayOff == true
            ? null
            : assignment?.ShiftId ?? await ReadGuidSettingAsync(SettingKeys.AttendanceDefaultShiftId, cancellationToken)
                .ConfigureAwait(false);

        return (isWeekend, isHoliday, assignment?.IsDayOff == true, shiftId);
    }

    private async Task<AttendanceRulesDto> LoadRulesAsync(CancellationToken cancellationToken)
    {
        var s = await LoadRuleSettingsAsync(cancellationToken).ConfigureAwait(false);
        var accuracy = await ReadIntSettingAsync(SettingKeys.AttendanceLocationAccuracyM, 100, cancellationToken)
            .ConfigureAwait(false);

        return new AttendanceRulesDto(
            s.GraceLateMinutes,
            s.GraceEarlyLeaveMinutes,
            s.AbsentAfterStartHours,
            s.OvertimeEnabled,
            s.OvertimeAfterEndMinutes,
            s.OvertimeMinMinutes,
            accuracy);
    }

    private async Task<AttendanceRuleSettings> LoadRuleSettingsAsync(CancellationToken cancellationToken)
    {
        var d = AttendanceRuleSettings.Defaults;
        return new AttendanceRuleSettings(
            await ReadIntSettingAsync(SettingKeys.AttendanceGraceLateMinutes, d.GraceLateMinutes, cancellationToken)
                .ConfigureAwait(false),
            await ReadIntSettingAsync(
                    SettingKeys.AttendanceGraceEarlyLeaveMinutes,
                    d.GraceEarlyLeaveMinutes,
                    cancellationToken)
                .ConfigureAwait(false),
            await ReadIntSettingAsync(
                    SettingKeys.AttendanceAbsentAfterStartHours,
                    d.AbsentAfterStartHours,
                    cancellationToken)
                .ConfigureAwait(false),
            await ReadBoolSettingAsync(SettingKeys.AttendanceOvertimeEnabled, d.OvertimeEnabled, cancellationToken)
                .ConfigureAwait(false),
            await ReadIntSettingAsync(
                    SettingKeys.AttendanceOvertimeAfterEndMinutes,
                    d.OvertimeAfterEndMinutes,
                    cancellationToken)
                .ConfigureAwait(false),
            await ReadIntSettingAsync(
                    SettingKeys.AttendanceOvertimeMinMinutes,
                    d.OvertimeMinMinutes,
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private async Task UpsertSettingAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return;
        }

        var json = value is string s && (s.StartsWith('[') || s.StartsWith('{') || s is "true" or "false")
            ? s
            : JsonSerializer.Serialize(value);

        // Guid? null → empty / null json string without quotes confusion
        if (value is null)
        {
            json = "null";
        }
        else if (value is string str && !str.StartsWith('[') && !str.StartsWith('{') && str is not ("true" or "false"))
        {
            json = JsonSerializer.Serialize(str);
        }

        var existing = await db.TenantSettings
            .SingleOrDefaultAsync(t => t.Key == key, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            db.TenantSettings.Add(TenantSetting.Create(tenantContext.TenantId.Value, key, json));
        }
        else
        {
            existing.SetValue(json);
        }
    }

    private async Task<int> ReadIntSettingAsync(string key, int fallback, CancellationToken cancellationToken)
    {
        var raw = await db.TenantSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.ValueJson)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<int>(raw, JsonOptions);
        }
        catch
        {
            return int.TryParse(raw.Trim('"'), out var n) ? n : fallback;
        }
    }

    private async Task<bool> ReadBoolSettingAsync(string key, bool fallback, CancellationToken cancellationToken)
    {
        var raw = await db.TenantSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.ValueJson)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<bool>(raw, JsonOptions);
        }
        catch
        {
            return bool.TryParse(raw.Trim('"'), out var b) ? b : fallback;
        }
    }

    private async Task<Guid?> ReadGuidSettingAsync(string key, CancellationToken cancellationToken)
    {
        var raw = await db.TenantSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.ValueJson)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw) || raw is "null")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Guid?>(raw, JsonOptions);
        }
        catch
        {
            return Guid.TryParse(raw.Trim('"'), out var g) ? g : null;
        }
    }

    private async Task<string[]> ReadStringArraySettingAsync(
        string key,
        string[] fallback,
        CancellationToken cancellationToken)
    {
        var raw = await db.TenantSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.ValueJson)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(raw, JsonOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static AttendanceBranchGeoDto MapBranchGeo(Branch branch) =>
        new(
            branch.Id,
            branch.Name,
            branch.Latitude,
            branch.Longitude,
            branch.GeofenceRadiusMeters,
            branch.OutOfRangePolicy.ToString(),
            branch.HasLocationConfigured);

    private static HolidayDto MapHoliday(Holiday h) =>
        new(h.Id, h.StartsOn, h.EndsOn, h.NameAr, h.NameEn, h.IsOfficial, h.RepeatsYearly, h.IsActive);

    private async Task<Result> EnsureFeatureAsync(CancellationToken cancellationToken) =>
        await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Attendance, cancellationToken).ConfigureAwait(false);

    private async Task<Result> EnsureWritableFeatureAsync(CancellationToken cancellationToken)
    {
        var writable = await subscriptionGuard.EnsureWritableAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return writable;
        }

        return await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
    }
}
