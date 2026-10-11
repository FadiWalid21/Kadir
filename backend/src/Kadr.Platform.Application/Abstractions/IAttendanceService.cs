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
}
