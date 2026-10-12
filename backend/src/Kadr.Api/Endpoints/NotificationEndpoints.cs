using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Notifications;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/notifications", ListAsync)
            .RequireAuthorization(PermissionCodes.NotificationsRead)
            .WithName("NotificationsList");

        app.MapGet("/api/v1/notifications/unread-count", UnreadCountAsync)
            .RequireAuthorization(PermissionCodes.NotificationsRead)
            .WithName("NotificationsUnreadCount");

        app.MapPost("/api/v1/notifications/{id:guid}/read", MarkReadAsync)
            .RequireAuthorization(PermissionCodes.NotificationsRead)
            .WithName("NotificationsMarkRead");

        app.MapPost("/api/v1/notifications/read-all", MarkAllReadAsync)
            .RequireAuthorization(PermissionCodes.NotificationsRead)
            .WithName("NotificationsMarkAllRead");

        app.MapGet("/api/v1/notifications/preferences", GetPreferencesAsync)
            .RequireAuthorization(PermissionCodes.NotificationsManagePrefs)
            .WithName("NotificationsGetPreferences");

        app.MapPut("/api/v1/notifications/preferences", UpdatePreferencesAsync)
            .RequireAuthorization(PermissionCodes.NotificationsManagePrefs)
            .WithName("NotificationsUpdatePreferences");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        INotificationService notifications,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false) =>
        ToResult(
            httpContext,
            await notifications.ListAsync(page, pageSize, unreadOnly, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> UnreadCountAsync(
        HttpContext httpContext,
        INotificationService notifications,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await notifications.GetUnreadCountAsync(cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> MarkReadAsync(
        Guid id,
        HttpContext httpContext,
        INotificationService notifications,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await notifications.MarkReadAsync(id, cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> MarkAllReadAsync(
        HttpContext httpContext,
        INotificationService notifications,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await notifications.MarkAllReadAsync(cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> GetPreferencesAsync(
        HttpContext httpContext,
        INotificationService notifications,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await notifications.GetPreferencesAsync(cancellationToken).ConfigureAwait(false),
            localizer);

    private static async Task<IResult> UpdatePreferencesAsync(
        [FromBody] UpdateNotificationPreferencesRequest request,
        HttpContext httpContext,
        INotificationService notifications,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken) =>
        ToResult(
            httpContext,
            await notifications.UpdatePreferencesAsync(request, cancellationToken).ConfigureAwait(false),
            localizer);

    private static IResult ToResult<T>(HttpContext httpContext, Result<T> result, IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.NotificationsValidationFailed, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        var status = code switch
        {
            ErrorCodes.NotificationsNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.NotificationsPushNotConfigured => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
