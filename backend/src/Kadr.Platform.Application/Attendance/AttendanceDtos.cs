namespace Kadr.Platform.Application.Attendance;

public sealed record PunchRequest(
    double Latitude,
    double Longitude,
    double AccuracyM,
    long PositionAgeMs,
    Guid ClientRequestId,
    string? Reason);

public sealed record PunchResultDto(
    Guid PunchId,
    string Kind,
    DateTimeOffset PunchedAt,
    DateOnly WorkDate,
    bool IsInside,
    double DistanceM,
    string? OutsideReason,
    string DayStatus);

public sealed record AttendancePunchDto(
    Guid Id,
    string Kind,
    DateTimeOffset PunchedAt,
    bool IsInside,
    double? DistanceM,
    string? OutsideReason);

public sealed record AttendanceShiftDto(
    Guid? Id,
    string? NameAr,
    string? NameEn,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool CrossesMidnight);

public sealed record AttendanceBranchGeoDto(
    Guid Id,
    string Name,
    double? Latitude,
    double? Longitude,
    int? GeofenceRadiusMeters,
    string OutOfRangePolicy,
    bool IsConfigured);

public sealed record AttendanceTodayDto(
    DateOnly WorkDate,
    string DayKind,
    AttendanceShiftDto? Shift,
    AttendanceBranchGeoDto Branch,
    AttendancePunchDto? OpenPunch,
    IReadOnlyList<AttendancePunchDto> Punches,
    string? DayStatus,
    int? LateMinutes,
    bool CanPunch);

public sealed record AttendanceMonthDayDto(
    DateOnly Date,
    string Status,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes,
    DateTimeOffset? FirstInAt,
    DateTimeOffset? LastOutAt);

public sealed record AttendanceMonthDto(
    int Year,
    int Month,
    IReadOnlyList<AttendanceMonthDayDto> Days,
    int PresentDays,
    int LateDays,
    int AbsentDays,
    int IncompleteDays,
    int TotalWorkedMinutes,
    int TotalLateMinutes,
    int TotalOvertimeMinutes);

public sealed record AttendanceRulesDto(
    int GraceLateMinutes,
    int GraceEarlyLeaveMinutes,
    int AbsentAfterStartHours,
    bool OvertimeEnabled,
    int OvertimeAfterEndMinutes,
    int OvertimeMinMinutes,
    int LocationAccuracyM);

public sealed record UpdateAttendanceRulesRequest(
    int GraceLateMinutes,
    int GraceEarlyLeaveMinutes,
    int AbsentAfterStartHours,
    bool OvertimeEnabled,
    int OvertimeAfterEndMinutes,
    int OvertimeMinMinutes,
    int LocationAccuracyM);

public sealed record AttendanceDefaultShiftDto(Guid? ShiftId, string? NameAr, string? NameEn);

public sealed record SetDefaultShiftRequest(Guid? ShiftId);

public sealed record UpdateBranchLocationRequest(
    double Latitude,
    double Longitude,
    int GeofenceRadiusMeters,
    string OutOfRangePolicy);

public sealed record HolidayDto(
    Guid Id,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string NameAr,
    string NameEn,
    bool IsOfficial,
    bool RepeatsYearly,
    bool IsActive);

public sealed record UpsertHolidayRequest(
    DateOnly StartsOn,
    DateOnly EndsOn,
    string NameAr,
    string NameEn,
    bool IsOfficial,
    bool RepeatsYearly,
    bool IsActive = true);

public sealed record WeekendSettingsDto(IReadOnlyList<string> WeekendDays);

public sealed record UpdateWeekendRequest(IReadOnlyList<string> WeekendDays);

public sealed record BranchLocationDto(
    Guid BranchId,
    string Name,
    double? Latitude,
    double? Longitude,
    int? GeofenceRadiusMeters,
    string OutOfRangePolicy,
    bool IsConfigured);
