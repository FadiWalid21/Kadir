using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Identity.Application.Auth;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Auth;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Application.Subscriptions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me", GetMeAsync)
            .RequireAuthorization()
            .WithName("GetMe");

        app.MapGet("/api/v1/me/sign-in-methods", GetSignInMethodsAsync)
            .RequireAuthorization()
            .WithName("GetSignInMethods");

        app.MapPost("/api/v1/me/google/link", LinkGoogleAsync)
            .RequireAuthorization()
            .RequireRateLimiting("me-security")
            .WithName("LinkGoogle");

        app.MapDelete("/api/v1/me/google", UnlinkGoogleAsync)
            .RequireAuthorization()
            .RequireRateLimiting("me-security")
            .WithName("UnlinkGoogle");

        app.MapPost("/api/v1/me/password", SetPasswordAsync)
            .RequireAuthorization()
            .RequireRateLimiting("me-security")
            .WithName("SetPassword");

        app.MapGet("/api/v1/me/push-subscriptions/vapid-public-key", GetVapidPublicKeyAsync)
            .RequireAuthorization()
            .RequireRateLimiting("me-push")
            .WithName("MePushVapidPublicKey");

        app.MapGet("/api/v1/me/push-subscriptions", ListPushSubscriptionsAsync)
            .RequireAuthorization()
            .RequireRateLimiting("me-push")
            .WithName("MePushList");

        app.MapPost("/api/v1/me/push-subscriptions", UpsertPushSubscriptionAsync)
            .RequireAuthorization()
            .RequireRateLimiting("me-push")
            .WithName("MePushUpsert");

        app.MapDelete("/api/v1/me/push-subscriptions/{id:guid}", DeletePushSubscriptionAsync)
            .RequireAuthorization()
            .RequireRateLimiting("me-push")
            .WithName("MePushDelete");

        app.MapPost("/api/v1/me/push-subscriptions/test", SendPushTestAsync)
            .RequireAuthorization()
            .RequireRateLimiting("me-push-test")
            .WithName("MePushTest");

        return app;
    }

    private static IResult GetVapidPublicKeyAsync(
        HttpContext httpContext,
        IPushSubscriptionService pushSubscriptions,
        IErrorLocalizer localizer)
    {
        var result = pushSubscriptions.GetVapidPublicKey();
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToPushProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> ListPushSubscriptionsAsync(
        HttpContext httpContext,
        IPushSubscriptionService pushSubscriptions,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await pushSubscriptions.ListAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToPushProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> UpsertPushSubscriptionAsync(
        [FromBody] UpsertPushSubscriptionRequest request,
        HttpContext httpContext,
        IPushSubscriptionService pushSubscriptions,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (userAgent.Length > 512)
        {
            userAgent = userAgent[..512];
        }

        var result = await pushSubscriptions
            .UpsertAsync(request, string.IsNullOrWhiteSpace(userAgent) ? null : userAgent, cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToPushProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> DeletePushSubscriptionAsync(
        Guid id,
        HttpContext httpContext,
        IPushSubscriptionService pushSubscriptions,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await pushSubscriptions.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.NoContent()
            : ToPushProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> SendPushTestAsync(
        HttpContext httpContext,
        IPushSubscriptionService pushSubscriptions,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await pushSubscriptions.SendTestAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.NoContent()
            : ToPushProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static IResult ToPushProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        var status = code switch
        {
            ErrorCodes.NotificationsPushNotConfigured => StatusCodes.Status503ServiceUnavailable,
            ErrorCodes.NotificationsPushSubscriptionNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.NotificationsPushLimitExceeded => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }

    private static async Task<IResult> GetSignInMethodsAsync(
        HttpContext httpContext,
        ISignInMethodsService signInMethods,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await signInMethods.GetAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> LinkGoogleAsync(
        [FromBody] LinkGoogleRequest request,
        HttpContext httpContext,
        ISignInMethodsService signInMethods,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await signInMethods
            .LinkGoogleAsync(
                request.Credential,
                RefreshCookie.Read(httpContext.Request),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess ? Results.NoContent() : ToProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> UnlinkGoogleAsync(
        HttpContext httpContext,
        ISignInMethodsService signInMethods,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await signInMethods
            .UnlinkGoogleAsync(RefreshCookie.Read(httpContext.Request), cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess ? Results.NoContent() : ToProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> SetPasswordAsync(
        [FromBody] SetPasswordRequest request,
        HttpContext httpContext,
        ISignInMethodsService signInMethods,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await signInMethods
            .SetPasswordAsync(
                request.CurrentPassword,
                request.NewPassword,
                RefreshCookie.Read(httpContext.Request),
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess ? Results.NoContent() : ToProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        // Google sign-in switched off: the endpoint behaves as if it does not exist.
        if (code == ErrorCodes.AuthGoogleDisabled)
        {
            return Results.NotFound();
        }

        // 401 is reserved for an expired session, so rejected input on these endpoints is 400/409.
        var status = code switch
        {
            ErrorCodes.AuthGoogleAlreadyLinked or ErrorCodes.AuthLastSignInMethod => StatusCodes.Status409Conflict,
            ErrorCodes.UsersNotFound => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }

    private static async Task<IResult> GetMeAsync(
        ITenantContext tenantContext,
        KadrDbContext db,
        IPermissionService permissions,
        ISubscriptionService subscriptions,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Results.Unauthorized();
        }

        var tenantId = tenantContext.TenantId.Value;
        var userId = tenantContext.UserId.Value;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.FullName, u.Email, u.PreferredLanguage })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Id, t.Name, t.Slug, t.LogoFileId })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var roles = await permissions.GetRoleNamesAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var perms = await permissions.GetPermissionsAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var logoUrl = tenant.LogoFileId is null ? null : $"/api/v1/files/{tenant.LogoFileId:D}";

        var photoFileId = await db.Employees.AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => e.PhotoFileId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var photoUrl = photoFileId is null ? null : $"/api/v1/files/{photoFileId.Value:D}";

        var now = DateTimeOffset.UtcNow;
        var inviteTeamDone = await db.Users.AsNoTracking()
                .AnyAsync(u => u.Id != userId, cancellationToken)
                .ConfigureAwait(false)
            || await db.Invitations.AsNoTracking()
                .AnyAsync(
                    i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now,
                    cancellationToken)
                .ConfigureAwait(false);

        MeSubscriptionSummary? subscription = null;
        var subResult = await subscriptions.GetMeSummaryAsync(cancellationToken).ConfigureAwait(false);
        if (subResult.IsSuccess)
        {
            subscription = subResult.Value;
        }

        return Results.Ok(new MeResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.PreferredLanguage,
            photoUrl,
            roles,
            perms,
            new MeTenant(tenant.Id, tenant.Name, tenant.Slug, logoUrl),
            new MeChecklist(inviteTeamDone),
            subscription));
    }
}

public sealed record MeResponse(
    Guid Id,
    string FullName,
    string Email,
    string Language,
    string? PhotoUrl,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    MeTenant Tenant,
    MeChecklist Checklist,
    MeSubscriptionSummary? Subscription);

public sealed record MeTenant(Guid Id, string Name, string Slug, string? LogoUrl);

public sealed record MeChecklist(bool InviteTeamDone);
