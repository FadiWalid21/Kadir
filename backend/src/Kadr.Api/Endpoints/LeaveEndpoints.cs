using Kadr.Api.Filters;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Leave;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class LeaveEndpoints
{
    public static IEndpointRouteBuilder MapLeaveEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/leave/types", ListTypesAsync)
            .RequireAuthorization(PermissionCodes.LeaveManage)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveListTypes");

        app.MapGet("/api/v1/leave/types/active", ListActiveTypesAsync)
            .RequireAuthorization(PermissionCodes.LeaveReadOwn)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveListActiveTypes");

        app.MapPut("/api/v1/leave/types/{id:guid}", UpdateTypeAsync)
            .RequireAuthorization(PermissionCodes.LeaveManage)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveUpdateType");

        app.MapGet("/api/v1/leave/balances/me", GetMyBalancesAsync)
            .RequireAuthorization(PermissionCodes.LeaveReadOwn)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveMyBalances");

        app.MapGet("/api/v1/leave/balances", ListBalancesAsync)
            .RequireAuthorization(PermissionCodes.LeaveReadAll)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveListBalances");

        app.MapPost("/api/v1/leave/balances/adjust", AdjustBalanceAsync)
            .RequireAuthorization(PermissionCodes.LeaveManage)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveAdjustBalance");

        app.MapPost("/api/v1/leave/requests", CreateLeaveAsync)
            .RequireAuthorization(PermissionCodes.LeaveRequest)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveCreateRequest");

        app.MapGet("/api/v1/leave/requests/mine", ListMyLeaveAsync)
            .RequireAuthorization(PermissionCodes.LeaveReadOwn)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveListMine");

        app.MapGet("/api/v1/leave/requests/{id:guid}", GetLeaveAsync)
            .RequireAuthorization(PermissionCodes.LeaveReadOwn)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveGetRequest");

        app.MapPost("/api/v1/leave/requests/{id:guid}/cancel", CancelLeaveAsync)
            .RequireAuthorization(PermissionCodes.LeaveRequest)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveCancelRequest");

        app.MapPost("/api/v1/leave/permissions", CreatePermissionAsync)
            .RequireAuthorization(PermissionCodes.LeaveRequest)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveCreatePermission");

        app.MapGet("/api/v1/leave/permissions/mine", ListMyPermissionsAsync)
            .RequireAuthorization(PermissionCodes.LeaveReadOwn)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("LeaveListMyPermissions");

        app.MapGet("/api/v1/requests/inbox", ListInboxAsync)
            .RequireAuthorization(PermissionCodes.LeaveReadTeam)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("RequestsInbox");

        app.MapPost("/api/v1/requests/{taskId:guid}/approve", ApproveAsync)
            .RequireAuthorization(PermissionCodes.LeaveApprove)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("RequestsApprove");

        app.MapPost("/api/v1/requests/{taskId:guid}/reject", RejectAsync)
            .RequireAuthorization(PermissionCodes.LeaveApprove)
            .RequireFeature(FeatureKeys.Leave)
            .WithName("RequestsReject");

        app.MapGet("/api/v1/workflows", ListWorkflowsAsync)
            .RequireAuthorization(PermissionCodes.WorkflowsManage)
            .RequireFeature(FeatureKeys.Workflows)
            .WithName("WorkflowsList");

        app.MapPost("/api/v1/workflows", CreateWorkflowAsync)
            .RequireAuthorization(PermissionCodes.WorkflowsManage)
            .RequireFeature(FeatureKeys.Workflows)
            .WithName("WorkflowsCreate");

        app.MapPut("/api/v1/workflows/{id:guid}", UpdateWorkflowAsync)
            .RequireAuthorization(PermissionCodes.WorkflowsManage)
            .RequireFeature(FeatureKeys.Workflows)
            .WithName("WorkflowsUpdate");

        return app;
    }

    private static async Task<IResult> ListTypesAsync(
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(httpContext, await leave.ListLeaveTypesAsync(cancellationToken).ConfigureAwait(false), localizer);

    private static async Task<IResult> ListActiveTypesAsync(
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await leave.ListLeaveTypesAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return ToProblem(httpContext, result.ErrorCode!, localizer);
        }

        return Results.Ok(result.Value!.Where(t => t.IsActive).ToList());
    }

    private static async Task<IResult> UpdateTypeAsync(
        Guid id,
        [FromBody] UpdateLeaveTypeRequest request,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await leave.UpdateLeaveTypeAsync(id, request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetMyBalancesAsync(
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] int? year = null) =>
        ToResult(httpContext, await leave.GetMyBalancesAsync(year, cancellationToken).ConfigureAwait(false), localizer);

    private static async Task<IResult> ListBalancesAsync(
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] int? year = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] Guid? leaveTypeId = null) =>
        ToResult(
            httpContext,
            await leave.ListBalancesAsync(year, employeeId, leaveTypeId, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> AdjustBalanceAsync(
        [FromBody] AdjustLeaveBalanceRequest request,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await leave.AdjustBalanceAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> CreateLeaveAsync(
        [FromBody] CreateLeaveRequestBody request,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await leave.CreateLeaveRequestAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ListMyLeaveAsync(
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await leave.ListMyLeaveRequestsAsync(status, page, pageSize, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetLeaveAsync(
        Guid id,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(httpContext, await leave.GetLeaveRequestAsync(id, cancellationToken).ConfigureAwait(false), localizer);

    private static async Task<IResult> CancelLeaveAsync(
        Guid id,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await leave.CancelLeaveRequestAsync(id, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> CreatePermissionAsync(
        [FromBody] CreatePermissionRequestBody request,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await leave.CreatePermissionAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ListMyPermissionsAsync(
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await leave.ListMyPermissionsAsync(cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ListInboxAsync(
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        ToResult(
            httpContext,
            await leave.ListInboxAsync(page, pageSize, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> ApproveAsync(
        Guid taskId,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await leave.ApproveTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.NoContent()
            : ToProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> RejectAsync(
        Guid taskId,
        [FromBody] RejectRequestBody request,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await leave.RejectTaskAsync(taskId, request, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.NoContent()
            : ToProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> ListWorkflowsAsync(
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(httpContext, await leave.ListWorkflowsAsync(cancellationToken).ConfigureAwait(false), localizer);

    private static async Task<IResult> CreateWorkflowAsync(
        [FromBody] UpsertWorkflowRequest request,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await leave.UpsertWorkflowAsync(null, request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> UpdateWorkflowAsync(
        Guid id,
        [FromBody] UpsertWorkflowRequest request,
        HttpContext httpContext,
        ILeaveService leave,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await leave.UpsertWorkflowAsync(id, request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static IResult ToResult<T>(HttpContext httpContext, Result<T> result, IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.LeaveValidationFailed, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        var status = code switch
        {
            ErrorCodes.LeaveNotFound or ErrorCodes.WorkflowsNotFound or ErrorCodes.RequestsNotFound =>
                StatusCodes.Status404NotFound,
            ErrorCodes.RequestsForbidden or ErrorCodes.PlanFeatureNotAvailable or ErrorCodes.SubscriptionExpired =>
                StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
