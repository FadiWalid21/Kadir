using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Identity.Application.Auth;
using Kadr.Platform.Application.PlatformAdmin;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kadr.Api.Endpoints;

public static class PlatformAuthEndpoints
{
    public static IEndpointRouteBuilder MapPlatformAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/platform/auth");

        group.MapPost("/login", LoginAsync)
            .RequireRateLimiting("auth-login")
            .WithName("PlatformLogin");

        group.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting("auth-refresh")
            .WithName("PlatformRefresh");

        group.MapPost("/logout", LogoutAsync)
            .RequireRateLimiting("auth-refresh")
            .WithName("PlatformLogout");

        app.MapGet("/api/v1/platform/me", MeAsync)
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
            .WithName("PlatformMe");

        return app;
    }

    private static IResult MeAsync(HttpContext httpContext, Kadr.SharedKernel.Tenancy.ITenantContext tenantContext)
    {
        if (!tenantContext.IsPlatformAdmin || tenantContext.UserId is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new PlatformMeResponse(tenantContext.UserId.Value));
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] PlatformLoginRequest request,
        HttpContext httpContext,
        IPlatformAuthService auth,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(
                request.Email,
                request.Password,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status401Unauthorized,
                result.ErrorCode ?? ErrorCodes.AuthInvalidCredentials,
                localizer));
        }

        var secure = httpContext.Request.IsHttps;
        PlatformRefreshCookie.Set(
            httpContext.Response,
            result.Value!.RefreshOpaqueToken,
            result.Value.RefreshExpiresAt,
            secure);

        return Results.Ok(new AccessTokenResponse(
            result.Value.AccessToken,
            result.Value.ExpiresInSeconds,
            TokenType: "Bearer"));
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext httpContext,
        IPlatformAuthService auth,
        IJwtTokenService jwtTokenService,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (!RefreshCookie.HasCsrfHeader(httpContext.Request))
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext, StatusCodes.Status400BadRequest, ErrorCodes.AuthCsrfHeaderRequired, localizer));
        }

        var opaque = PlatformRefreshCookie.Read(httpContext.Request);
        if (string.IsNullOrWhiteSpace(opaque))
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext, StatusCodes.Status401Unauthorized, ErrorCodes.AuthRefreshInvalid, localizer));
        }

        var rotated = await auth.RefreshAsync(
                opaque,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken)
            .ConfigureAwait(false);

        var secure = httpContext.Request.IsHttps;
        if (!rotated.IsSuccess)
        {
            PlatformRefreshCookie.Clear(httpContext.Response, secure);
            return Results.Problem(ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status401Unauthorized,
                rotated.ErrorCode ?? ErrorCodes.AuthRefreshInvalid,
                localizer));
        }

        var access = jwtTokenService.CreatePlatformAccessToken(
            rotated.Value!.PlatformAdminId,
            rotated.Value.Language);

        PlatformRefreshCookie.Set(
            httpContext.Response,
            rotated.Value.OpaqueToken,
            rotated.Value.ExpiresAt,
            secure);

        return Results.Ok(new AccessTokenResponse(access.AccessToken, access.ExpiresInSeconds, "Bearer"));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        IPlatformAuthService auth,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (!RefreshCookie.HasCsrfHeader(httpContext.Request))
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext, StatusCodes.Status400BadRequest, ErrorCodes.AuthCsrfHeaderRequired, localizer));
        }

        var opaque = PlatformRefreshCookie.Read(httpContext.Request);
        await auth.LogoutAsync(opaque, cancellationToken).ConfigureAwait(false);
        PlatformRefreshCookie.Clear(httpContext.Response, httpContext.Request.IsHttps);
        return Results.NoContent();
    }
}

public sealed record PlatformLoginRequest(string Email, string Password);

public sealed record PlatformMeResponse(Guid Id);
