using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Auth;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kadr.Api.Endpoints;

public static class GoogleAuthEndpoints
{
    public static IEndpointRouteBuilder MapGoogleAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/auth/providers", GetProvidersAsync)
            .RequireRateLimiting("auth-providers")
            .WithName("GetAuthProviders");

        app.MapPost("/api/v1/auth/google/nonce", IssueNonceAsync)
            .RequireRateLimiting("auth-google-nonce")
            .WithName("IssueGoogleNonce");

        app.MapPost("/api/v1/auth/google/login", LoginAsync)
            .RequireRateLimiting("auth-google-login")
            .WithName("GoogleLogin");

        app.MapPost("/api/v1/auth/google/select-tenant", SelectTenantAsync)
            .RequireRateLimiting("auth-google-select")
            .WithName("GoogleSelectTenant");

        return app;
    }

    private static IResult GetProvidersAsync(IGoogleAuthService google)
    {
        return Results.Ok(google.GetProviders());
    }

    private static async Task<IResult> IssueNonceAsync(
        HttpContext httpContext,
        IGoogleAuthService google,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await google.IssueNonceAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return ToDisabledOrProblem(httpContext, result.ErrorCode!, localizer);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] GoogleLoginRequest request,
        HttpContext httpContext,
        IGoogleAuthService google,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await google
            .LoginAsync(
                request.Credential,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return ToLoginProblem(httpContext, result.ErrorCode!, localizer);
        }

        return result.Value switch
        {
            GoogleLoginSuccessDto success => OkWithCookie(httpContext, success),
            GoogleTenantSelectionDto selection => Results.Ok(selection),
            GoogleNotFoundDto notFound => Results.Problem(CreateNotFoundProblem(httpContext, notFound, localizer)),
            _ => Results.Problem(ApiProblemDetails.Create(
                httpContext, StatusCodes.Status500InternalServerError, ErrorCodes.AuthGoogleTokenInvalid, localizer)),
        };
    }

    private static async Task<IResult> SelectTenantAsync(
        [FromBody] GoogleSelectTenantRequest request,
        HttpContext httpContext,
        IGoogleAuthService google,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await google
            .SelectTenantAsync(
                request.SelectionToken,
                request.TenantId,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return ToLoginProblem(httpContext, result.ErrorCode!, localizer);
        }

        return OkWithCookie(httpContext, result.Value!);
    }

    private static IResult OkWithCookie(HttpContext httpContext, GoogleLoginSuccessDto success)
    {
        var secure = httpContext.Request.IsHttps;
        RefreshCookie.Set(httpContext.Response, success.RefreshOpaqueToken, success.RefreshExpiresAt, secure);
        return Results.Ok(new AccessTokenResponse(
            success.AccessToken,
            success.ExpiresInSeconds,
            success.TokenType)); // AccessTokenResponse.ExpiresIn
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails CreateNotFoundProblem(
        HttpContext httpContext,
        GoogleNotFoundDto notFound,
        IErrorLocalizer localizer)
    {
        var problem = ApiProblemDetails.Create(
            httpContext, StatusCodes.Status404NotFound, notFound.Code, localizer);
        problem.Extensions["registrationToken"] = notFound.RegistrationToken;
        return problem;
    }

    private static IResult ToDisabledOrProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        if (code == ErrorCodes.AuthGoogleDisabled)
        {
            return Results.NotFound();
        }

        return Results.Problem(ApiProblemDetails.Create(
            httpContext, StatusCodes.Status400BadRequest, code, localizer));
    }

    private static IResult ToLoginProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        if (code == ErrorCodes.AuthGoogleDisabled)
        {
            return Results.NotFound();
        }

        var status = code switch
        {
            ErrorCodes.AuthEmailNotVerified
                or ErrorCodes.AuthGoogleEmailUnverified
                or ErrorCodes.AuthAccountDisabled => StatusCodes.Status403Forbidden,
            ErrorCodes.AuthGoogleAccountNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.AuthGoogleNonceInvalid
                or ErrorCodes.AuthGoogleTokenInvalid
                or ErrorCodes.AuthGoogleSelectionInvalid => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status401Unauthorized,
        };

        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
