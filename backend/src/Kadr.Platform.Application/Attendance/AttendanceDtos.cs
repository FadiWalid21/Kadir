namespace Kadr.Platform.Application.Attendance;

public sealed record PunchRequest(
    double Latitude,
    double Longitude,
    double AccuracyM,
    long PositionAgeMs,
    Guid ClientRequestId,
    string? Reason,
    Guid? PhotoFileId = null);

public sealed record PunchResultDto(
    Guid PunchId,
    string Kind,
    DateTimeOffset PunchedAt,
    DateOnly WorkDate,
    bool IsInside,
    double DistanceM,
    string? OutsideReason,
    string DayStatus,
    Guid? WorkLocationId = null);

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

// --- F16b: HR today board ---

public sealed record AttendanceBoardStatsDto(
    int Present,
    int Late,
    int Absent,
    int OnLeave,
    int NotYet,
    int Total);

public sealed record AttendanceBoardRowDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullNameAr,
    string FullNameEn,
    string? JobTitleAr,
    string? JobTitleEn,
    string BranchName,
    Guid BranchId,
    Guid? DepartmentId,
    string? DepartmentNameAr,
    string? DepartmentNameEn,
    string Status,
    DateTimeOffset? FirstInAt,
    DateTimeOffset? LastOutAt,
    int LateMinutes,
    int WorkedMinutes,
    bool IsInsideGeofence,
    double? DistanceM,
    string? ShiftNameAr,
    string? ShiftNameEn,
    TimeOnly? ShiftStart,
    TimeOnly? ShiftEnd);

public sealed record AttendanceBoardDto(
    DateOnly Date,
    string DayKind,
    AttendanceBoardStatsDto Stats,
    IReadOnlyList<AttendanceBoardRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

// --- F16c: schedules ---

public sealed record ShiftAssignmentCellDto(
    Guid EmployeeId,
    DateOnly WorkDate,
    Guid? ShiftId,
    string? ShiftNameAr,
    string? ShiftNameEn,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool IsDayOff,
    bool IsHoliday,
    bool HasConflict);

public sealed record ShiftAssignmentEmployeeDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullNameAr,
    string FullNameEn,
    string? JobTitleAr,
    string? JobTitleEn,
    IReadOnlyList<ShiftAssignmentCellDto> Days);

public sealed record ShiftScheduleWeekDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    IReadOnlyList<ShiftAssignmentEmployeeDto> Employees,
    int ScheduledShifts,
    int RestDays,
    int Conflicts);

public sealed record UpsertShiftAssignmentRequest(
    Guid EmployeeId,
    DateOnly WorkDate,
    Guid? ShiftId,
    bool IsDayOff);

public sealed record BulkUpsertShiftAssignmentsRequest(
    IReadOnlyList<UpsertShiftAssignmentRequest> Items);

// --- F16d: reports matrix ---

public sealed record AttendanceMatrixDayCellDto(DateOnly Date, string Status, int LateMinutes);

public sealed record AttendanceMatrixRowDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullNameAr,
    string FullNameEn,
    string? JobTitleAr,
    string? JobTitleEn,
    int PresentDays,
    IReadOnlyList<AttendanceMatrixDayCellDto> Days);

public sealed record AttendanceMatrixDto(
    int Year,
    int Month,
    double CommitmentPercent,
    int TotalLateMinutes,
    int TotalOvertimeMinutes,
    IReadOnlyList<AttendanceMatrixRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

// --- F16e: correction requests ---

public sealed record AttendanceCorrectionRequestDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNameAr,
    string EmployeeNameEn,
    DateOnly WorkDate,
    string ProblemType,
    TimeOnly ProposedTime,
    string Reason,
    Guid? AttachmentFileId,
    string Status,
    string? RejectReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt,
    string? DecidedByName);

public sealed record CreateAttendanceCorrectionRequest(
    DateOnly WorkDate,
    string ProblemType,
    TimeOnly ProposedTime,
    string Reason,
    Guid? AttachmentFileId);

public sealed record RejectAttendanceCorrectionRequest(string Reason);

public sealed record PagedAttendanceCorrectionsDto(
    IReadOnlyList<AttendanceCorrectionRequestDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int PendingCount);

public sealed record WorkLocationDto(
    Guid Id,
    string NameAr,
    string? NameEn,
    string Type,
    Guid? LinkedBranchId,
    double Latitude,
    double Longitude,
    int GeofenceRadiusMeters,
    string? Address,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsActive);

public sealed record UpsertWorkLocationRequest(
    string NameAr,
    string? NameEn,
    string Type,
    Guid? LinkedBranchId,
    double Latitude,
    double Longitude,
    int GeofenceRadiusMeters,
    string? Address,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsActive = true);

public sealed record PagedWorkLocationsDto(
    IReadOnlyList<WorkLocationDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record WorkLocationAssignmentDto(
    Guid Id,
    string Scope,
    Guid? EmployeeId,
    Guid? DepartmentId,
    Guid? BranchId,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record WorkLocationAssignmentInput(
    string Scope,
    Guid? EmployeeId,
    Guid? DepartmentId,
    Guid? BranchId,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record ReplaceWorkLocationAssignmentsRequest(
    IReadOnlyList<WorkLocationAssignmentInput> Assignments);

public sealed record EmployeeAttendancePolicyDto(
    Guid EmployeeId,
    bool MayClockInFromAnywhere,
    bool AnywhereRequiresReason,
    bool AnywhereRequiresPhoto);

public sealed record UpdateEmployeeAttendancePolicyRequest(
    bool MayClockInFromAnywhere,
    bool AnywhereRequiresReason,
    bool AnywhereRequiresPhoto);
