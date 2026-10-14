using Kadr.Infrastructure.Persistence.Employees;
using Kadr.Platform.Application.Attendance;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Templates;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Attendance;

public sealed partial class AttendanceService
{
    private const int MaxPageSize = 100;

    public async Task<Result<AttendanceBoardDto>> GetBoardTodayAsync(
        DateOnly? date,
        Guid? branchId,
        Guid? departmentId,
        string? status,
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<AttendanceBoardDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<AttendanceBoardDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        var tenant = await db.Tenants.AsNoTracking()
            .SingleAsync(t => t.Id == tenantContext.TenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        var workDate = date ?? TenantTimeZones.ToWorkDate(DateTimeOffset.UtcNow, tenant.TimeZone);

        var scope = await EmployeeDataScope.ResolveAsync(db, tenantContext.UserId.Value, cancellationToken)
            .ConfigureAwait(false);
        var query = EmployeeDataScope.Apply(
            db.Employees.AsNoTracking(),
            scope.Scope,
            tenantContext.UserId.Value,
            scope.LinkedEmployeeId,
            scope.DepartmentId,
            scope.BranchId);

        query = query.Where(e =>
            e.Status == EmployeeStatus.Active
            || e.Status == EmployeeStatus.Probation
            || e.Status == EmployeeStatus.OnLeave);

        if (branchId is not null)
        {
            query = query.Where(e => e.BranchId == branchId);
        }

        if (departmentId is not null)
        {
            query = query.Where(e => e.DepartmentId == departmentId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(e => e.SearchName.Contains(term));
        }

        var employeeIds = await query.Select(e => e.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        var existingSummaries = await db.AttendanceDaySummaries.AsNoTracking()
            .Where(s => s.WorkDate == workDate && employeeIds.Contains(s.EmployeeId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byEmp = existingSummaries.ToDictionary(s => s.EmployeeId);

        var present = existingSummaries.Count(s => s.Status == AttendanceDayStatus.Present);
        var late = existingSummaries.Count(s => s.Status == AttendanceDayStatus.Late);
        var absent = existingSummaries.Count(s => s.Status == AttendanceDayStatus.Absent);
        var onLeave = existingSummaries.Count(s => s.Status == AttendanceDayStatus.Leave);
        var notYet = employeeIds.Count(id =>
        {
            if (!byEmp.TryGetValue(id, out var s))
            {
                return true;
            }

            return s.FirstInAt is null
                && s.Status is not AttendanceDayStatus.Weekend
                    and not AttendanceDayStatus.Holiday
                    and not AttendanceDayStatus.Absent
                    and not AttendanceDayStatus.Leave;
        });

        var dayKind = "Workday";
        if (existingSummaries.Count > 0
            && existingSummaries.All(s => s.Status == AttendanceDayStatus.Holiday))
        {
            dayKind = "Holiday";
        }
        else if (existingSummaries.Count > 0
                 && existingSummaries.All(s =>
                     s.Status is AttendanceDayStatus.Weekend or AttendanceDayStatus.Holiday))
        {
            dayKind = "Weekend";
        }

        var stats = new AttendanceBoardStatsDto(
            present,
            late,
            absent,
            onLeave,
            notYet,
            employeeIds.Count);

        // Filter by status for list
        IEnumerable<Guid> filteredIds = employeeIds;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var key = status.Trim();
            filteredIds = key.ToLowerInvariant() switch
            {
                "present" => employeeIds.Where(id =>
                    byEmp.TryGetValue(id, out var s)
                    && s.Status is AttendanceDayStatus.Present
                        or AttendanceDayStatus.Late
                        or AttendanceDayStatus.Incomplete),
                "late" => employeeIds.Where(id =>
                    byEmp.TryGetValue(id, out var s) && s.Status == AttendanceDayStatus.Late),
                "absent" => employeeIds.Where(id =>
                    byEmp.TryGetValue(id, out var s) && s.Status == AttendanceDayStatus.Absent),
                "leave" => employeeIds.Where(id =>
                    byEmp.TryGetValue(id, out var s) && s.Status == AttendanceDayStatus.Leave),
                "notyet" => employeeIds.Where(id =>
                    !byEmp.TryGetValue(id, out var s)
                    || (s.FirstInAt is null
                        && s.Status is not AttendanceDayStatus.Weekend
                            and not AttendanceDayStatus.Holiday
                            and not AttendanceDayStatus.Absent)),
                _ => employeeIds,
            };
        }

        var filteredList = filteredIds.ToList();
        var totalCount = filteredList.Count;
        var pageIds = filteredList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        foreach (var id in pageIds.Where(id => !byEmp.ContainsKey(id)))
        {
            var summary = await EnsureSummaryAsync(
                    tenant.Id,
                    id,
                    workDate,
                    tenant.TimeZone,
                    cancellationToken)
                .ConfigureAwait(false);
            byEmp[id] = summary;
        }

        var pageEmployees = await (
                from e in db.Employees.AsNoTracking()
                where pageIds.Contains(e.Id)
                join b in db.Branches.AsNoTracking() on e.BranchId equals b.Id
                join d in db.Departments.AsNoTracking() on e.DepartmentId equals d.Id into dj
                from d in dj.DefaultIfEmpty()
                join j in db.JobTitles.AsNoTracking() on e.JobTitleId equals j.Id into jj
                from j in jj.DefaultIfEmpty()
                select new
                {
                    e.Id,
                    e.EmployeeNumber,
                    e.FullNameAr,
                    e.FullNameEn,
                    e.BranchId,
                    BranchName = b.NameAr,
                    DepartmentId = (Guid?)d.Id,
                    DepartmentNameAr = d != null ? d.NameAr : null,
                    DepartmentNameEn = d != null ? d.NameEn : null,
                    JobTitleAr = j != null ? j.NameAr : null,
                    JobTitleEn = j != null ? j.NameEn : null,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var punches = await db.AttendancePunches.AsNoTracking()
            .Where(p => pageIds.Contains(p.EmployeeId) && p.WorkDate == workDate)
            .OrderBy(p => p.PunchedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var lastInByEmp = punches
            .Where(p => p.Kind == PunchKind.In)
            .GroupBy(p => p.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Last());

        var shiftIds = byEmp.Values.Where(s => s.ShiftId != null).Select(s => s.ShiftId!.Value).Distinct().ToList();
        var shifts = await db.Shifts.AsNoTracking()
            .Where(s => shiftIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken)
            .ConfigureAwait(false);

        var items = pageEmployees
            .OrderBy(e => e.FullNameAr)
            .Select(e =>
            {
                byEmp.TryGetValue(e.Id, out var summary);
                lastInByEmp.TryGetValue(e.Id, out var lastIn);
                Shift? shift = null;
                if (summary?.ShiftId is Guid sid)
                {
                    shifts.TryGetValue(sid, out shift);
                }

                var statusName = summary?.Status.ToString() ?? "NotYet";
                if (summary is null)
                {
                    statusName = "NotYet";
                }

                return new AttendanceBoardRowDto(
                    e.Id,
                    e.EmployeeNumber,
                    e.FullNameAr,
                    e.FullNameEn,
                    e.JobTitleAr,
                    e.JobTitleEn,
                    e.BranchName,
                    e.BranchId,
                    e.DepartmentId,
                    e.DepartmentNameAr,
                    e.DepartmentNameEn,
                    statusName,
                    summary?.FirstInAt,
                    summary?.LastOutAt,
                    summary?.LateMinutes ?? 0,
                    summary?.WorkedMinutes ?? 0,
                    lastIn?.IsInside ?? false,
                    lastIn?.DistanceM,
                    shift?.NameAr,
                    shift?.NameEn,
                    shift?.StartTime,
                    shift?.EndTime);
            })
            .ToList();

        return Result.Success(new AttendanceBoardDto(
            workDate,
            dayKind,
            stats,
            items,
            page,
            pageSize,
            totalCount));
    }

    public async Task<Result<ShiftScheduleWeekDto>> GetScheduleWeekAsync(
        DateOnly weekStart,
        Guid? branchId,
        Guid? departmentId,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<ShiftScheduleWeekDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<ShiftScheduleWeekDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        // Normalize to Saturday-start week (EG common)
        while (weekStart.DayOfWeek != DayOfWeek.Saturday)
        {
            weekStart = weekStart.AddDays(-1);
        }

        var weekEnd = weekStart.AddDays(6);
        var tenant = await db.Tenants.AsNoTracking()
            .SingleAsync(t => t.Id == tenantContext.TenantId.Value, cancellationToken)
            .ConfigureAwait(false);

        var scope = await EmployeeDataScope.ResolveAsync(db, tenantContext.UserId.Value, cancellationToken)
            .ConfigureAwait(false);
        var query = EmployeeDataScope.Apply(
            db.Employees.AsNoTracking(),
            scope.Scope,
            tenantContext.UserId.Value,
            scope.LinkedEmployeeId,
            scope.DepartmentId,
            scope.BranchId);
        query = query.Where(e =>
            e.Status == EmployeeStatus.Active || e.Status == EmployeeStatus.Probation);
        if (branchId is not null)
        {
            query = query.Where(e => e.BranchId == branchId);
        }

        if (departmentId is not null)
        {
            query = query.Where(e => e.DepartmentId == departmentId);
        }

        var employees = await (
                from e in query
                join j in db.JobTitles.AsNoTracking() on e.JobTitleId equals j.Id into jj
                from j in jj.DefaultIfEmpty()
                orderby e.FullNameAr
                select new
                {
                    e.Id,
                    e.EmployeeNumber,
                    e.FullNameAr,
                    e.FullNameEn,
                    JobTitleAr = j != null ? j.NameAr : null,
                    JobTitleEn = j != null ? j.NameEn : null,
                })
            .Take(MaxPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = employees.Select(e => e.Id).ToList();
        var assignments = await db.ShiftAssignments.AsNoTracking()
            .Where(a => ids.Contains(a.EmployeeId) && a.WorkDate >= weekStart && a.WorkDate <= weekEnd)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var shiftIds = assignments.Where(a => a.ShiftId != null).Select(a => a.ShiftId!.Value).Distinct().ToList();
        var shifts = await db.Shifts.AsNoTracking()
            .Where(s => shiftIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken)
            .ConfigureAwait(false);

        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.IsActive && h.StartsOn <= weekEnd && h.EndsOn >= weekStart)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var weekend = await GetWeekendAsync(cancellationToken).ConfigureAwait(false);
        var weekendDays = weekend.IsSuccess
            ? weekend.Value!.WeekendDays.Select(ParseWeekendDay).Where(d => d != null).Select(d => d!.Value).ToHashSet()
            : new HashSet<DayOfWeek> { DayOfWeek.Friday, DayOfWeek.Saturday };

        var scheduled = 0;
        var rest = 0;
        var conflicts = 0;
        var rows = new List<ShiftAssignmentEmployeeDto>();

        foreach (var emp in employees)
        {
            var days = new List<ShiftAssignmentCellDto>();
            for (var d = weekStart; d <= weekEnd; d = d.AddDays(1))
            {
                var isHoliday = holidays.Any(h => h.StartsOn <= d && h.EndsOn >= d);
                var isWeekend = weekendDays.Contains(d.DayOfWeek);
                var a = assignments.FirstOrDefault(x => x.EmployeeId == emp.Id && x.WorkDate == d);
                Shift? shift = null;
                if (a?.ShiftId is Guid sid)
                {
                    shifts.TryGetValue(sid, out shift);
                }

                var isDayOff = a?.IsDayOff == true || (a is null && (isWeekend || isHoliday));
                if (a is not null && !a.IsDayOff)
                {
                    scheduled++;
                }
                else
                {
                    rest++;
                }

                // Conflict: multiple assignments shouldn't happen (unique index); flag holiday+shift
                var hasConflict = a is { IsDayOff: false } && isHoliday;
                if (hasConflict)
                {
                    conflicts++;
                }

                days.Add(new ShiftAssignmentCellDto(
                    emp.Id,
                    d,
                    a?.ShiftId,
                    shift?.NameAr,
                    shift?.NameEn,
                    shift?.StartTime,
                    shift?.EndTime,
                    isDayOff,
                    isHoliday,
                    hasConflict));
            }

            rows.Add(new ShiftAssignmentEmployeeDto(
                emp.Id,
                emp.EmployeeNumber,
                emp.FullNameAr,
                emp.FullNameEn,
                emp.JobTitleAr,
                emp.JobTitleEn,
                days));
        }

        _ = tenant;
        return Result.Success(new ShiftScheduleWeekDto(
            weekStart,
            weekEnd,
            rows,
            scheduled,
            rest,
            conflicts));
    }

    public async Task<Result<ShiftScheduleWeekDto>> UpsertShiftAssignmentsAsync(
        BulkUpsertShiftAssignmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<ShiftScheduleWeekDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<ShiftScheduleWeekDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (request.Items.Count == 0 || request.Items.Count > 500)
        {
            return Result.Failure<ShiftScheduleWeekDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        var tenantId = tenantContext.TenantId.Value;
        var min = request.Items.Min(i => i.WorkDate);
        var max = request.Items.Max(i => i.WorkDate);

        foreach (var item in request.Items)
        {
            if (!item.IsDayOff && item.ShiftId is null)
            {
                return Result.Failure<ShiftScheduleWeekDto>(
                    ErrorCodes.AttendanceValidationFailed,
                    ErrorCodes.AttendanceValidationFailed);
            }

            var existing = await db.ShiftAssignments
                .SingleOrDefaultAsync(
                    a => a.EmployeeId == item.EmployeeId && a.WorkDate == item.WorkDate,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existing is null)
            {
                db.ShiftAssignments.Add(ShiftAssignment.Create(
                    tenantId,
                    item.EmployeeId,
                    item.WorkDate,
                    item.ShiftId,
                    item.IsDayOff));
            }
            else
            {
                existing.Reassign(item.ShiftId, item.IsDayOff);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync(
                tenantId,
                tenantContext.UserId,
                "attendance.schedule.upsert",
                nameof(ShiftAssignment),
                null,
                null,
                $"{{\"count\":{request.Items.Count}}}",
                cancellationToken)
            .ConfigureAwait(false);

        return await GetScheduleWeekAsync(min, null, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AttendanceMatrixDto>> GetAttendanceMatrixAsync(
        int year,
        int month,
        Guid? branchId,
        Guid? departmentId,
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<AttendanceMatrixDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<AttendanceMatrixDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (year < 2000 || year > 2100 || month is < 1 or > 12)
        {
            return Result.Failure<AttendanceMatrixDto>(
                ErrorCodes.AttendanceValidationFailed,
                ErrorCodes.AttendanceValidationFailed);
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var scope = await EmployeeDataScope.ResolveAsync(db, tenantContext.UserId.Value, cancellationToken)
            .ConfigureAwait(false);
        var query = EmployeeDataScope.Apply(
            db.Employees.AsNoTracking(),
            scope.Scope,
            tenantContext.UserId.Value,
            scope.LinkedEmployeeId,
            scope.DepartmentId,
            scope.BranchId);
        query = query.Where(e =>
            e.Status == EmployeeStatus.Active
            || e.Status == EmployeeStatus.Probation
            || e.Status == EmployeeStatus.OnLeave);
        if (branchId is not null)
        {
            query = query.Where(e => e.BranchId == branchId);
        }

        if (departmentId is not null)
        {
            query = query.Where(e => e.DepartmentId == departmentId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(e => e.SearchName.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var employees = await (
                from e in query
                join j in db.JobTitles.AsNoTracking() on e.JobTitleId equals j.Id into jj
                from j in jj.DefaultIfEmpty()
                orderby e.FullNameAr
                select new
                {
                    e.Id,
                    e.EmployeeNumber,
                    e.FullNameAr,
                    e.FullNameEn,
                    JobTitleAr = j != null ? j.NameAr : null,
                    JobTitleEn = j != null ? j.NameEn : null,
                })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = employees.Select(e => e.Id).ToList();
        var allIds = await query.Select(e => e.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        var summaries = await db.AttendanceDaySummaries.AsNoTracking()
            .Where(s => allIds.Contains(s.EmployeeId) && s.WorkDate >= start && s.WorkDate <= end)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var workdayStatuses = summaries
            .Where(s => s.Status is not AttendanceDayStatus.Weekend and not AttendanceDayStatus.Holiday and not AttendanceDayStatus.NoShift)
            .ToList();
        var presentLike = workdayStatuses.Count(s =>
            s.Status is AttendanceDayStatus.Present or AttendanceDayStatus.Late);
        var commitment = workdayStatuses.Count == 0
            ? 100.0
            : Math.Round(100.0 * presentLike / workdayStatuses.Count, 1);
        var totalLate = summaries.Sum(s => s.LateMinutes);
        var totalOt = summaries.Sum(s => s.OvertimeMinutes);

        var pageSummaries = summaries.Where(s => ids.Contains(s.EmployeeId)).ToList();
        var rows = employees.Select(e =>
        {
            var days = pageSummaries
                .Where(s => s.EmployeeId == e.Id)
                .OrderBy(s => s.WorkDate)
                .Select(s => new AttendanceMatrixDayCellDto(s.WorkDate, s.Status.ToString(), s.LateMinutes))
                .ToList();
            var presentDays = days.Count(d =>
                d.Status is nameof(AttendanceDayStatus.Present) or nameof(AttendanceDayStatus.Late));
            return new AttendanceMatrixRowDto(
                e.Id,
                e.EmployeeNumber,
                e.FullNameAr,
                e.FullNameEn,
                e.JobTitleAr,
                e.JobTitleEn,
                presentDays,
                days);
        }).ToList();

        return Result.Success(new AttendanceMatrixDto(
            year,
            month,
            commitment,
            totalLate,
            totalOt,
            rows,
            page,
            pageSize,
            totalCount));
    }

    private static DayOfWeek? ParseWeekendDay(string day) => day.ToLowerInvariant() switch
    {
        "sun" => DayOfWeek.Sunday,
        "mon" => DayOfWeek.Monday,
        "tue" => DayOfWeek.Tuesday,
        "wed" => DayOfWeek.Wednesday,
        "thu" => DayOfWeek.Thursday,
        "fri" => DayOfWeek.Friday,
        "sat" => DayOfWeek.Saturday,
        _ => null,
    };
}
