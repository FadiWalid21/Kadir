using System.Text.Json;
using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Identity.Application.Auth;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Invitations;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kadr.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/register", RegisterAsync)
            .RequireRateLimiting("auth-register")
            .WithName("RegisterTenant");

        app.MapPost("/api/v1/auth/login", LoginAsync)
            .RequireRateLimiting("auth-login")
            .WithName("Login");

        app.MapPost("/api/v1/auth/refresh", RefreshAsync)
            .RequireRateLimiting("auth-refresh")
            .WithName("Refresh");

        app.MapPost("/api/v1/auth/logout", LogoutAsync)
            .RequireRateLimiting("auth-refresh")
            .WithName("Logout");

        app.MapPost("/api/v1/auth/verify-email", VerifyEmailAsync)
            .RequireRateLimiting("auth-verify")
            .WithName("VerifyEmail");

        app.MapPost("/api/v1/auth/resend-verification", ResendVerificationAsync)
            .RequireRateLimiting("auth-resend")
            .WithName("ResendVerification");

        app.MapPost("/api/v1/auth/invitations/validate", ValidateInvitationAsync)
            .RequireRateLimiting("auth-invite-validate")
            .WithName("ValidateInvitation");

        app.MapPost("/api/v1/auth/accept-invitation", AcceptInvitationAsync)
            .RequireRateLimiting("auth-invite-validate")
            .WithName("AcceptInvitation");

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterTenantRequest request,
        HttpContext httpContext,
        ICommandDispatcher dispatcher,
        IValidator<RegisterTenantCommand> validator,
        IdempotencyService idempotency,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = httpContext.Request.Headers[HttpHeaderNames.IdempotencyKey].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext, StatusCodes.Status400BadRequest, ErrorCodes.IdempotencyKeyRequired, localizer));
        }

        var requestHash = IdempotencyService.HashRequest(JsonSerializer.Serialize(request));
        var existing = await idempotency.FindAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return Results.Problem(ApiProblemDetails.Create(
                    httpContext, StatusCodes.Status409Conflict, ErrorCodes.IdempotencyKeyReuse, localizer));
            }

            return Results.Content(existing.ResponseBody, "application/json", statusCode: existing.ResponseStatus);
        }

        var command = new RegisterTenantCommand(
            request.FullName,
            request.Email,
            request.Password,
            request.CompanyName,
            request.IndustryCode,
            request.TimeZone,
            request.DefaultLanguage,
            request.CountryCode,
            request.WorkWeekDays,
            request.WorkWeekWeekend,
            request.DefaultWorkStart,
            request.DefaultWorkEnd,
            request.FirstBranchName,
            idempotencyKey);

        var validation = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            var code = validation.Errors.Any(e => e.ErrorCode == ErrorCodes.IndustryInvalid)
                ? ErrorCodes.IndustryInvalid
                : ErrorCodes.AuthValidationFailed;
            var problem = ApiProblemDetails.Create(
                httpContext, StatusCodes.Status400BadRequest, code, localizer);
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.Problem(problem);
        }

        var result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            var code = result.ErrorCode ?? ErrorCodes.RegistrationFailed;
            var status = code is ErrorCodes.AuthEmailTaken or ErrorCodes.IdempotencyKeyReuse
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
        }

        var body = new RegisterTenantResponse(
            result.Value!.TenantId,
            result.Value.Slug,
            result.Value.TrialEndsAt);

        await idempotency.StoreAsync(idempotencyKey, requestHash, StatusCodes.Status201Created, body, cancellationToken)
            .ConfigureAwait(false);

        return Results.Created($"/api/v1/tenants/{body.TenantId}", body);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        HttpContext httpContext,
        ICommandDispatcher dispatcher,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
                new LoginCommand(
                    request.Email,
                    request.Password,
                    request.CompanySlug,
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString()),
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var code = result.ErrorCode ?? ErrorCodes.AuthInvalidCredentials;
            var status = code switch
            {
                ErrorCodes.AuthEmailNotVerified => StatusCodes.Status403Forbidden,
                ErrorCodes.AuthAccountLocked => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status401Unauthorized,
            };
            return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
        }

        var secure = httpContext.Request.IsHttps;
        RefreshCookie.Set(httpContext.Response, result.Value!.RefreshOpaqueToken, result.Value.RefreshExpiresAt, secure);
        return Results.Ok(new AccessTokenResponse(
            result.Value.AccessToken,
            result.Value.ExpiresInSeconds,
            "Bearer"));
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext httpContext,
        IRefreshTokenService refreshTokenService,
        IJwtTokenService jwtTokenService,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (!RefreshCookie.HasCsrfHeader(httpContext.Request))
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext, StatusCodes.Status400BadRequest, ErrorCodes.AuthCsrfHeaderRequired, localizer));
        }

        var opaque = RefreshCookie.Read(httpContext.Request);
        if (string.IsNullOrWhiteSpace(opaque))
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext, StatusCodes.Status401Unauthorized, ErrorCodes.AuthRefreshInvalid, localizer));
        }

        var rotated = await refreshTokenService.RotateAsync(
                opaque,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken)
            .ConfigureAwait(false);

        var secure = httpContext.Request.IsHttps;
        if (!rotated.IsSuccess)
        {
            RefreshCookie.Clear(httpContext.Response, secure);
            var code = rotated.ErrorCode ?? ErrorCodes.AuthRefreshInvalid;
            return Results.Problem(ApiProblemDetails.Create(
                httpContext, StatusCodes.Status401Unauthorized, code, localizer));
        }

        var access = jwtTokenService.CreateAccessToken(
            rotated.Value!.UserId,
            rotated.Value.TenantId,
            rotated.Value.Roles,
            rotated.Value.Language);

        RefreshCookie.Set(httpContext.Response, rotated.Value.OpaqueToken, rotated.Value.ExpiresAt, secure);
        return Results.Ok(new AccessTokenResponse(access.AccessToken, access.ExpiresInSeconds, "Bearer"));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        IRefreshTokenService refreshTokenService,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (!RefreshCookie.HasCsrfHeader(httpContext.Request))
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext, StatusCodes.Status400BadRequest, ErrorCodes.AuthCsrfHeaderRequired, localizer));
        }

        var opaque = RefreshCookie.Read(httpContext.Request);
        if (!string.IsNullOrWhiteSpace(opaque))
        {
            await refreshTokenService.RevokeFamilyByOpaqueTokenAsync(opaque, cancellationToken)
                .ConfigureAwait(false);
        }

        RefreshCookie.Clear(httpContext.Response, httpContext.Request.IsHttps);
        return Results.NoContent();
    }

    private static async Task<IResult> VerifyEmailAsync(
        [FromBody] VerifyEmailRequest request,
        HttpContext httpContext,
        ICommandDispatcher dispatcher,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new VerifyEmailCommand(request.Token), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? ErrorCodes.AuthVerificationInvalid,
                localizer));
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ResendVerificationAsync(
        [FromBody] ResendVerificationRequest request,
        HttpContext httpContext,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        await dispatcher.SendAsync(
                new ResendVerificationCommand(request.Email, request.CompanySlug),
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Accepted();
    }

    private static async Task<IResult> ValidateInvitationAsync(
        [FromBody] ValidateInvitationRequest request,
        IInvitationService invitations,
        CancellationToken cancellationToken)
    {
        var result = await invitations.ValidateAsync(request.Token, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> AcceptInvitationAsync(
        [FromBody] AcceptInvitationRequest request,
        HttpContext httpContext,
        IInvitationService invitations,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await invitations.AcceptAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        var code = result.ErrorCode ?? ErrorCodes.InvitationInvalid;
        var status = code switch
        {
            ErrorCodes.AuthEmailTaken => StatusCodes.Status409Conflict,
            ErrorCodes.AuthPasswordTooShort or ErrorCodes.AuthPasswordTooCommon => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}

public sealed record RegisterTenantRequest(
    string FullName,
    string Email,
    string Password,
    string CompanyName,
    string IndustryCode,
    string? TimeZone,
    string? DefaultLanguage,
    string? CountryCode,
    IReadOnlyList<string>? WorkWeekDays = null,
    IReadOnlyList<string>? WorkWeekWeekend = null,
    string? DefaultWorkStart = null,
    string? DefaultWorkEnd = null,
    string? FirstBranchName = null);

public sealed record RegisterTenantResponse(Guid TenantId, string Slug, DateTimeOffset TrialEndsAt);

public sealed record LoginRequest(string Email, string Password, string CompanySlug);

public sealed record AccessTokenResponse(string AccessToken, int ExpiresIn, string TokenType);

public sealed record VerifyEmailRequest(string Token);

public sealed record ResendVerificationRequest(string Email, string CompanySlug);
