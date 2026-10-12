using Kadr.Platform.Application.Attendance;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IAttendanceService
{
    Task<Result<PunchResultDto>> PunchAsync(PunchRequest request, CancellationToken cancellationToken = default);

    Task<Result<AttendanceTodayDto>> GetMyTodayAsync(CancellationToken cancellationToken = default);

    Task<Result<AttendanceMonthDto>> GetMyMonthAsync(int year, int month, CancellationToken cancellationToken = default);

    Task<Result<AttendanceRulesDto>> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<Result<AttendanceRulesDto>> UpdateRulesAsync(
        UpdateAttendanceRulesRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceDefaultShiftDto>> GetDefaultShiftAsync(CancellationToken cancellationToken = default);

    Task<Result<AttendanceDefaultShiftDto>> SetDefaultShiftAsync(
        SetDefaultShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BranchLocationDto>>> ListBranchLocationsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<BranchLocationDto>> UpdateBranchLocationAsync(
        Guid branchId,
        UpdateBranchLocationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HolidayDto>>> ListHolidaysAsync(
        int? year,
        CancellationToken cancellationToken = default);

    Task<Result<HolidayDto>> CreateHolidayAsync(
        UpsertHolidayRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<HolidayDto>> UpdateHolidayAsync(
        Guid id,
        UpsertHolidayRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteHolidayAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<WeekendSettingsDto>> GetWeekendAsync(CancellationToken cancellationToken = default);

    Task<Result<WeekendSettingsDto>> UpdateWeekendAsync(
        UpdateWeekendRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceBoardDto>> GetBoardTodayAsync(
        DateOnly? date,
        Guid? branchId,
        Guid? departmentId,
        string? status,
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<ShiftScheduleWeekDto>> GetScheduleWeekAsync(
        DateOnly weekStart,
        Guid? branchId,
        Guid? departmentId,
        CancellationToken cancellationToken = default);

    Task<Result<ShiftScheduleWeekDto>> UpsertShiftAssignmentsAsync(
        BulkUpsertShiftAssignmentsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceMatrixDto>> GetAttendanceMatrixAsync(
        int year,
        int month,
        Guid? branchId,
        Guid? departmentId,
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceCorrectionRequestDto>> CreateCorrectionAsync(
        CreateAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedAttendanceCorrectionsDto>> ListMyCorrectionsAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<PagedAttendanceCorrectionsDto>> ListCorrectionsInboxAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceCorrectionRequestDto>> ApproveCorrectionAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<AttendanceCorrectionRequestDto>> RejectCorrectionAsync(
        Guid id,
        RejectAttendanceCorrectionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedWorkLocationsDto>> ListWorkLocationsAsync(
        string? type,
        bool? active,
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<WorkLocationDto>> GetWorkLocationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<WorkLocationDto>> CreateWorkLocationAsync(
        UpsertWorkLocationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<WorkLocationDto>> UpdateWorkLocationAsync(
        Guid id,
        UpsertWorkLocationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkLocationAssignmentDto>>> GetWorkLocationAssignmentsAsync(
        Guid workLocationId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkLocationAssignmentDto>>> ReplaceWorkLocationAssignmentsAsync(
        Guid workLocationId,
        ReplaceWorkLocationAssignmentsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAttendancePolicyDto>> GetEmployeeAttendancePolicyAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAttendancePolicyDto>> UpdateEmployeeAttendancePolicyAsync(
        Guid employeeId,
        UpdateEmployeeAttendancePolicyRequest request,
        CancellationToken cancellationToken = default);
}
