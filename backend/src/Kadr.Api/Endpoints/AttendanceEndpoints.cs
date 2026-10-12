using Kadr.Api.Filters;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Attendance;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/attendance/punch", PunchAsync)
            .RequireAuthorization(PermissionCodes.AttendancePunch)
            .RequireFeature(FeatureKeys.Attendance)
            .RequireRateLimiting("attendance-punch")
            .WithName("AttendancePunch");

        app.MapGet("/api/v1/attendance/me/today", GetTodayAsync)
            .RequireAuthorization(PermissionCodes.AttendanceReadOwn)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceMeToday");

        app.MapGet("/api/v1/attendance/me/month", GetMonthAsync)
            .RequireAuthorization(PermissionCodes.AttendanceReadOwn)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceMeMonth");

        app.MapGet("/api/v1/attendance/settings/rules", GetRulesAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceGetRules");

        app.MapPut("/api/v1/attendance/settings/rules", UpdateRulesAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceUpdateRules");

        app.MapGet("/api/v1/attendance/settings/default-shift", GetDefaultShiftAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceGetDefaultShift");

        app.MapPut("/api/v1/attendance/settings/default-shift", SetDefaultShiftAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceSetDefaultShift");

        app.MapGet("/api/v1/attendance/settings/branches", ListBranchesAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceListBranches");

        app.MapPut("/api/v1/attendance/settings/branches/{branchId:guid}/location", UpdateBranchLocationAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceUpdateBranchLocation");

        app.MapGet("/api/v1/attendance/settings/holidays", ListHolidaysAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceListHolidays");

        app.MapPost("/api/v1/attendance/settings/holidays", CreateHolidayAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceCreateHoliday");

        app.MapPut("/api/v1/attendance/settings/holidays/{id:guid}", UpdateHolidayAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceUpdateHoliday");

        app.MapDelete("/api/v1/attendance/settings/holidays/{id:guid}", DeleteHolidayAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceDeleteHoliday");

        app.MapGet("/api/v1/attendance/settings/weekend", GetWeekendAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceGetWeekend");

        app.MapPut("/api/v1/attendance/settings/weekend", UpdateWeekendAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceUpdateWeekend");

        app.MapGet("/api/v1/attendance/today", GetBoardTodayAsync)
            .RequireAuthorization(PermissionCodes.AttendanceReadTeam)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceBoardToday");

        app.MapGet("/api/v1/attendance/schedules/week", GetScheduleWeekAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceScheduleWeek");

        app.MapPut("/api/v1/attendance/schedules/assignments", UpsertShiftAssignmentsAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceUpsertAssignments");

        app.MapGet("/api/v1/attendance/shift-rules", ListShiftRulesAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceListShiftRules");

        app.MapPost("/api/v1/attendance/shift-rules", CreateShiftRulesAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceCreateShiftRules");

        app.MapPost("/api/v1/attendance/shift-rules/preview", PreviewShiftRulesAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendancePreviewShiftRules");

        app.MapPut("/api/v1/attendance/shift-rules/{id:guid}", UpdateShiftRuleAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceUpdateShiftRule");

        app.MapPost("/api/v1/attendance/shift-rules/{id:guid}/end", EndShiftRuleAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceEndShiftRule");

        app.MapGet("/api/v1/attendance/reports/matrix", GetAttendanceMatrixAsync)
            .RequireAuthorization(PermissionCodes.AttendanceReports)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceReportsMatrix");

        app.MapPost("/api/v1/attendance/corrections", CreateCorrectionAsync)
            .RequireAuthorization(PermissionCodes.AttendancePunch)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceCreateCorrection");

        app.MapGet("/api/v1/attendance/corrections/mine", ListMyCorrectionsAsync)
            .RequireAuthorization(PermissionCodes.AttendanceReadOwn)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceListMyCorrections");

        app.MapGet("/api/v1/attendance/corrections", ListCorrectionsInboxAsync)
            .RequireAuthorization(PermissionCodes.AttendanceReadTeam)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceListCorrectionsInbox");

        app.MapPost("/api/v1/attendance/corrections/{id:guid}/approve", ApproveCorrectionAsync)
            .RequireAuthorization(PermissionCodes.AttendanceEdit)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceApproveCorrection");

        app.MapPost("/api/v1/attendance/corrections/{id:guid}/reject", RejectCorrectionAsync)
            .RequireAuthorization(PermissionCodes.AttendanceEdit)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceRejectCorrection");

        app.MapGet("/api/v1/attendance/work-locations", ListWorkLocationsAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceListWorkLocations");

        app.MapGet("/api/v1/attendance/work-locations/{id:guid}", GetWorkLocationAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceGetWorkLocation");

        app.MapPost("/api/v1/attendance/work-locations", CreateWorkLocationAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceCreateWorkLocation");

        app.MapPut("/api/v1/attendance/work-locations/{id:guid}", UpdateWorkLocationAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceUpdateWorkLocation");

        app.MapGet("/api/v1/attendance/work-locations/{id:guid}/assignments", GetWorkLocationAssignmentsAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceGetWorkLocationAssignments");

        app.MapPut("/api/v1/attendance/work-locations/{id:guid}/assignments", ReplaceWorkLocationAssignmentsAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceReplaceWorkLocationAssignments");

        app.MapGet("/api/v1/attendance/employees/{id:guid}/attendance-policy", GetEmployeeAttendancePolicyAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceGetEmployeePolicy");

        app.MapPut("/api/v1/attendance/employees/{id:guid}/attendance-policy", UpdateEmployeeAttendancePolicyAsync)
            .RequireAuthorization(PermissionCodes.AttendanceManage)
            .RequireFeature(FeatureKeys.Attendance)
            .WithName("AttendanceUpdateEmployeePolicy");

        return app;
    }

    private static async Task<IResult> PunchAsync(
        [FromBody] PunchRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(httpContext, await attendance.PunchAsync(request, cancellationToken).ConfigureAwait(false), localizer);

    private static async Task<IResult> GetTodayAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(httpContext, await attendance.GetMyTodayAsync(cancellationToken).ConfigureAwait(false), localizer);

    private static async Task<IResult> GetMonthAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] int year,
        [FromQuery] int month) =>
        ToResult(
            httpContext,
            await attendance.GetMyMonthAsync(year, month, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetBoardTodayAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? date = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await attendance.GetBoardTodayAsync(
                    date,
                    branchId,
                    departmentId,
                    status,
                    q,
                    page,
                    pageSize,
                    cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetScheduleWeekAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] DateOnly weekStart,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? departmentId = null) =>
        ToResult(
            httpContext,
            await attendance.GetScheduleWeekAsync(weekStart, branchId, departmentId, cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> UpsertShiftAssignmentsAsync(
        [FromBody] BulkUpsertShiftAssignmentsRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.UpsertShiftAssignmentsAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ListShiftRulesAsync(
        HttpContext httpContext,
        IShiftAssignmentRuleService rules,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? targetType = null) =>
        ToResult(
            httpContext,
            await rules.ListAsync(page, pageSize, targetType, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> CreateShiftRulesAsync(
        [FromBody] CreateShiftAssignmentRulesRequest request,
        HttpContext httpContext,
        IShiftAssignmentRuleService rules,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await rules.CreateAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> PreviewShiftRulesAsync(
        [FromBody] PreviewShiftAssignmentRulesRequest request,
        HttpContext httpContext,
        IShiftAssignmentRuleService rules,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await rules.PreviewAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> UpdateShiftRuleAsync(
        Guid id,
        [FromBody] UpdateShiftAssignmentRuleRequest request,
        HttpContext httpContext,
        IShiftAssignmentRuleService rules,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await rules.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> EndShiftRuleAsync(
        Guid id,
        [FromBody] EndShiftAssignmentRuleRequest request,
        HttpContext httpContext,
        IShiftAssignmentRuleService rules,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await rules.EndAsync(id, request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetAttendanceMatrixAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await attendance.GetAttendanceMatrixAsync(
                    year,
                    month,
                    branchId,
                    departmentId,
                    q,
                    page,
                    pageSize,
                    cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> CreateCorrectionAsync(
        [FromBody] CreateAttendanceCorrectionRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.CreateCorrectionAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ListMyCorrectionsAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await attendance.ListMyCorrectionsAsync(status, page, pageSize, cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ListCorrectionsInboxAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await attendance.ListCorrectionsInboxAsync(status, page, pageSize, cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ApproveCorrectionAsync(
        Guid id,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.ApproveCorrectionAsync(id, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> RejectCorrectionAsync(
        Guid id,
        [FromBody] RejectAttendanceCorrectionRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.RejectCorrectionAsync(id, request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetRulesAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(httpContext, await attendance.GetRulesAsync(cancellationToken).ConfigureAwait(false), localizer);

    private static async Task<IResult> UpdateRulesAsync(
        [FromBody] UpdateAttendanceRulesRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.UpdateRulesAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetDefaultShiftAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.GetDefaultShiftAsync(cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> SetDefaultShiftAsync(
        [FromBody] SetDefaultShiftRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.SetDefaultShiftAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ListBranchesAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.ListBranchLocationsAsync(cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> UpdateBranchLocationAsync(
        Guid branchId,
        [FromBody] UpdateBranchLocationRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.UpdateBranchLocationAsync(branchId, request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ListHolidaysAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] int? year = null) =>
        ToResult(
            httpContext,
            await attendance.ListHolidaysAsync(year, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> CreateHolidayAsync(
        [FromBody] UpsertHolidayRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.CreateHolidayAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> UpdateHolidayAsync(
        Guid id,
        [FromBody] UpsertHolidayRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.UpdateHolidayAsync(id, request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> DeleteHolidayAsync(
        Guid id,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await attendance.DeleteHolidayAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.AttendanceValidationFailed, localizer);
    }

    private static async Task<IResult> GetWeekendAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(httpContext, await attendance.GetWeekendAsync(cancellationToken).ConfigureAwait(false), localizer);

    private static async Task<IResult> UpdateWeekendAsync(
        [FromBody] UpdateWeekendRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.UpdateWeekendAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ListWorkLocationsAsync(
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? type = null,
        [FromQuery] bool? active = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await attendance.ListWorkLocationsAsync(type, active, q, page, pageSize, cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetWorkLocationAsync(
        Guid id,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.GetWorkLocationAsync(id, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> CreateWorkLocationAsync(
        [FromBody] UpsertWorkLocationRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.CreateWorkLocationAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> UpdateWorkLocationAsync(
        Guid id,
        [FromBody] UpsertWorkLocationRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.UpdateWorkLocationAsync(id, request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetWorkLocationAssignmentsAsync(
        Guid id,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.GetWorkLocationAssignmentsAsync(id, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ReplaceWorkLocationAssignmentsAsync(
        Guid id,
        [FromBody] ReplaceWorkLocationAssignmentsRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.ReplaceWorkLocationAssignmentsAsync(id, request, cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetEmployeeAttendancePolicyAsync(
        Guid id,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.GetEmployeeAttendancePolicyAsync(id, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> UpdateEmployeeAttendancePolicyAsync(
        Guid id,
        [FromBody] UpdateEmployeeAttendancePolicyRequest request,
        HttpContext httpContext,
        IAttendanceService attendance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await attendance.UpdateEmployeeAttendancePolicyAsync(id, request, cancellationToken)
                .ConfigureAwait(false),
            localizer);

    private static IResult ToResult<T>(HttpContext httpContext, Result<T> result, IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.AttendanceValidationFailed, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        var status = code switch
        {
            ErrorCodes.AttendanceNotFound
                or ErrorCodes.AttendanceLocationNotFound
                or ErrorCodes.AttendanceShiftRuleNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.PlanFeatureNotAvailable or ErrorCodes.AttendanceForbidden or ErrorCodes.SubscriptionExpired =>
                StatusCodes.Status403Forbidden,
            ErrorCodes.AttendanceDuplicatePunch or ErrorCodes.AttendanceShiftRuleOverlap => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
