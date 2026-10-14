using Kadr.Platform.Domain.Attendance;

namespace Kadr.Platform.Tests.Attendance;

public sealed class AttendanceRulesEngineTests
{
    private const string Tz = "Africa/Cairo";
    private static readonly AttendanceRuleSettings Rules = AttendanceRuleSettings.Defaults;
    private static readonly DateOnly Day = new(2026, 3, 15); // Sunday, not weekend in EG default

    private static ShiftWindow DayShift() =>
        AttendanceRulesEngine.BuildWindow(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Day,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            crossesMidnight: false,
            breakMinutes: 60,
            Tz);

    private static ShiftWindow NightShift(DateOnly workDate) =>
        AttendanceRulesEngine.BuildWindow(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            workDate,
            new TimeOnly(22, 0),
            new TimeOnly(6, 0),
            crossesMidnight: true,
            breakMinutes: 30,
            Tz);

    [Fact]
    public void Holiday_wins()
    {
        var r = AttendanceRulesEngine.Compute([], DayShift(), Rules, false, true, false, false);
        Assert.Equal(AttendanceDayStatus.Holiday, r.Status);
    }

    [Fact]
    public void Weekend_wins()
    {
        var r = AttendanceRulesEngine.Compute([], DayShift(), Rules, true, false, false, false);
        Assert.Equal(AttendanceDayStatus.Weekend, r.Status);
    }

    [Fact]
    public void No_shift()
    {
        var r = AttendanceRulesEngine.Compute([], null, Rules, false, false, false, true);
        Assert.Equal(AttendanceDayStatus.NoShift, r.Status);
    }

    [Fact]
    public void No_shift_with_in_only_is_incomplete_with_first_in()
    {
        var inAt = new DateTimeOffset(2026, 3, 15, 8, 0, 0, TimeSpan.Zero);
        var punches = new[] { new PunchFact(PunchKind.In, inAt) };
        var r = AttendanceRulesEngine.Compute(punches, null, Rules, false, false, false, true);
        Assert.Equal(AttendanceDayStatus.Incomplete, r.Status);
        Assert.Equal(inAt, r.FirstInAt);
        Assert.Null(r.LastOutAt);
    }

    [Fact]
    public void No_shift_with_in_and_out_is_present()
    {
        var inAt = new DateTimeOffset(2026, 3, 15, 8, 0, 0, TimeSpan.Zero);
        var outAt = inAt.AddHours(8);
        var punches = new[]
        {
            new PunchFact(PunchKind.In, inAt),
            new PunchFact(PunchKind.Out, outAt),
        };
        var r = AttendanceRulesEngine.Compute(punches, null, Rules, false, false, false, true);
        Assert.Equal(AttendanceDayStatus.Present, r.Status);
        Assert.Equal(inAt, r.FirstInAt);
        Assert.Equal(outAt, r.LastOutAt);
        Assert.Equal(480, r.WorkedMinutes);
    }

    [Fact]
    public void Present_on_time()
    {
        var w = DayShift();
        var punches = new[]
        {
            new PunchFact(PunchKind.In, w.ShiftStartUtc.AddMinutes(5)),
            new PunchFact(PunchKind.Out, w.ShiftEndUtc.AddMinutes(-5)),
        };
        var r = AttendanceRulesEngine.Compute(punches, w, Rules, false, false, false, false);
        Assert.Equal(AttendanceDayStatus.Present, r.Status);
        Assert.Equal(0, r.LateMinutes);
        Assert.True(r.WorkedMinutes > 0);
    }

    [Fact]
    public void Late_after_grace()
    {
        var w = DayShift();
        var punches = new[]
        {
            new PunchFact(PunchKind.In, w.ShiftStartUtc.AddMinutes(20)),
            new PunchFact(PunchKind.Out, w.ShiftEndUtc),
        };
        var r = AttendanceRulesEngine.Compute(punches, w, Rules, false, false, false, false);
        Assert.Equal(AttendanceDayStatus.Late, r.Status);
        Assert.True(r.LateMinutes >= 20);
    }

    [Fact]
    public void Missing_checkout_is_incomplete()
    {
        var w = DayShift();
        var punches = new[] { new PunchFact(PunchKind.In, w.ShiftStartUtc) };
        var r = AttendanceRulesEngine.Compute(punches, w, Rules, false, false, false, false);
        Assert.Equal(AttendanceDayStatus.Incomplete, r.Status);
    }

    [Fact]
    public void Early_leave_after_grace()
    {
        var w = DayShift();
        var punches = new[]
        {
            new PunchFact(PunchKind.In, w.ShiftStartUtc),
            new PunchFact(PunchKind.Out, w.ShiftEndUtc.AddMinutes(-30)),
        };
        var r = AttendanceRulesEngine.Compute(punches, w, Rules, false, false, false, false);
        Assert.True(r.EarlyLeaveMinutes >= 30);
    }

    [Fact]
    public void Overtime_counts_when_enabled()
    {
        var w = DayShift();
        var rules = Rules with { OvertimeEnabled = true, OvertimeAfterEndMinutes = 0, OvertimeMinMinutes = 15 };
        var punches = new[]
        {
            new PunchFact(PunchKind.In, w.ShiftStartUtc),
            new PunchFact(PunchKind.Out, w.ShiftEndUtc.AddMinutes(45)),
        };
        var r = AttendanceRulesEngine.Compute(punches, w, rules, false, false, false, false);
        Assert.True(r.OvertimeMinutes >= 45);
    }

    [Fact]
    public void Overnight_window_contains_late_night_and_early_morning()
    {
        var w = NightShift(Day);
        var lateNight = w.ShiftStartUtc.AddHours(1);
        var earlyMorning = w.ShiftEndUtc.AddHours(-1);
        Assert.True(AttendanceRulesEngine.Contains(w, lateNight));
        Assert.True(AttendanceRulesEngine.Contains(w, earlyMorning));
        Assert.True(w.CrossesMidnight);
    }

    [Fact]
    public void Cairo_midnight_boundary_work_date()
    {
        // 2026-03-15 22:30 Cairo = still work date 15 for night shift starting 22:00
        var local = new DateTime(2026, 3, 15, 22, 30, 0, DateTimeKind.Unspecified);
        var tz = TenantTimeZones.Resolve(Tz);
        var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, tz));
        Assert.Equal(new DateOnly(2026, 3, 15), TenantTimeZones.ToWorkDate(utc, Tz));

        var afterMidnightLocal = new DateTime(2026, 3, 16, 1, 0, 0, DateTimeKind.Unspecified);
        var afterUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(afterMidnightLocal, tz));
        Assert.Equal(new DateOnly(2026, 3, 16), TenantTimeZones.ToWorkDate(afterUtc, Tz));
    }

    [Fact]
    public void Absent_when_no_in_punch()
    {
        var r = AttendanceRulesEngine.Compute([], DayShift(), Rules, false, false, false, false);
        Assert.Equal(AttendanceDayStatus.Absent, r.Status);
    }
}
