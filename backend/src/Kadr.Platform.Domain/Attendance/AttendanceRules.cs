namespace Kadr.Platform.Domain.Attendance;

public sealed record AttendanceRuleSettings(
    int GraceLateMinutes,
    int GraceEarlyLeaveMinutes,
    int AbsentAfterStartHours,
    bool OvertimeEnabled,
    int OvertimeAfterEndMinutes,
    int OvertimeMinMinutes)
{
    public static AttendanceRuleSettings Defaults { get; } = new(
        GraceLateMinutes: 15,
        GraceEarlyLeaveMinutes: 10,
        AbsentAfterStartHours: 2,
        OvertimeEnabled: false,
        OvertimeAfterEndMinutes: 0,
        OvertimeMinMinutes: 30);
}

public sealed record ShiftWindow(
    Guid? ShiftId,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    DateTimeOffset ShiftStartUtc,
    DateTimeOffset ShiftEndUtc,
    int BreakMinutes,
    bool CrossesMidnight);

public sealed record PunchFact(PunchKind Kind, DateTimeOffset PunchedAtUtc);

public sealed record AttendanceRulesResult(
    AttendanceDayStatus Status,
    DateTimeOffset? FirstInAt,
    DateTimeOffset? LastOutAt,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes);

/// <summary>Pure day recomputation. No I/O.</summary>
public static class AttendanceRulesEngine
{
    public const int WindowStartLeadHours = 3;
    public const int WindowEndTrailHours = 6;

    public static ShiftWindow BuildWindow(
        Guid? shiftId,
        DateOnly workDate,
        TimeOnly start,
        TimeOnly end,
        bool crossesMidnight,
        int breakMinutes,
        string? timeZoneId)
    {
        var overnight = crossesMidnight || end <= start;
        var shiftStart = TenantTimeZones.ToUtc(workDate, start, timeZoneId);
        var endDate = overnight ? workDate.AddDays(1) : workDate;
        var shiftEnd = TenantTimeZones.ToUtc(endDate, end, timeZoneId);
        return new ShiftWindow(
            shiftId,
            shiftStart.AddHours(-WindowStartLeadHours),
            shiftEnd.AddHours(WindowEndTrailHours),
            shiftStart,
            shiftEnd,
            breakMinutes,
            overnight);
    }

    public static bool Contains(ShiftWindow window, DateTimeOffset punchedAtUtc) =>
        punchedAtUtc >= window.WindowStartUtc && punchedAtUtc <= window.WindowEndUtc;

    public static AttendanceRulesResult Compute(
        IReadOnlyList<PunchFact> punches,
        ShiftWindow? shift,
        AttendanceRuleSettings rules,
        bool isWeekend,
        bool isHoliday,
        bool isDayOff,
        bool hasNoShift)
    {
        if (isHoliday)
        {
            return Empty(AttendanceDayStatus.Holiday);
        }

        if (isWeekend || isDayOff)
        {
            return Empty(AttendanceDayStatus.Weekend);
        }

        if (hasNoShift || shift is null)
        {
            return Empty(AttendanceDayStatus.NoShift);
        }

        var ordered = punches.OrderBy(p => p.PunchedAtUtc).ToList();
        var firstIn = ordered.FirstOrDefault(p => p.Kind == PunchKind.In)?.PunchedAtUtc;
        var lastOut = ordered.LastOrDefault(p => p.Kind == PunchKind.Out)?.PunchedAtUtc;

        if (firstIn is null)
        {
            return Empty(AttendanceDayStatus.Absent);
        }

        var late = 0;
        var lateThreshold = shift.ShiftStartUtc.AddMinutes(rules.GraceLateMinutes);
        if (firstIn.Value > lateThreshold)
        {
            late = (int)Math.Ceiling((firstIn.Value - shift.ShiftStartUtc).TotalMinutes);
        }

        if (lastOut is null)
        {
            return new AttendanceRulesResult(
                AttendanceDayStatus.Incomplete,
                firstIn,
                null,
                0,
                late,
                0,
                0);
        }

        var rawWorked = (int)Math.Max(0, Math.Floor((lastOut.Value - firstIn.Value).TotalMinutes) - shift.BreakMinutes);
        var earlyLeave = 0;
        var earlyThreshold = shift.ShiftEndUtc.AddMinutes(-rules.GraceEarlyLeaveMinutes);
        if (lastOut.Value < earlyThreshold)
        {
            earlyLeave = (int)Math.Ceiling((shift.ShiftEndUtc - lastOut.Value).TotalMinutes);
        }

        var overtime = 0;
        if (rules.OvertimeEnabled)
        {
            var otStart = shift.ShiftEndUtc.AddMinutes(rules.OvertimeAfterEndMinutes);
            if (lastOut.Value > otStart)
            {
                var ot = (int)Math.Floor((lastOut.Value - otStart).TotalMinutes);
                if (ot >= rules.OvertimeMinMinutes)
                {
                    overtime = ot;
                }
            }
        }

        var status = late > 0 ? AttendanceDayStatus.Late : AttendanceDayStatus.Present;
        return new AttendanceRulesResult(status, firstIn, lastOut, rawWorked, late, earlyLeave, overtime);
    }

    private static AttendanceRulesResult Empty(AttendanceDayStatus status) =>
        new(status, null, null, 0, 0, 0, 0);
}
