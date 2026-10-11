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
            ErrorCodes.AttendanceNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.PlanFeatureNotAvailable or ErrorCodes.AttendanceForbidden or ErrorCodes.SubscriptionExpired =>
                StatusCodes.Status403Forbidden,
            ErrorCodes.AttendanceDuplicatePunch => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
