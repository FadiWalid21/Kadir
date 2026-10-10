using System.Text.Json;
using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
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
            idempotencyKey);

        var validation = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            var problem = ApiProblemDetails.Create(
                httpContext, StatusCodes.Status400BadRequest, ErrorCodes.AuthValidationFailed, localizer);
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
}

public sealed record RegisterTenantRequest(
    string FullName,
    string Email,
    string Password,
    string CompanyName,
    string IndustryCode,
    string? TimeZone,
    string? DefaultLanguage,
    string? CountryCode);

public sealed record RegisterTenantResponse(Guid TenantId, string Slug, DateTimeOffset TrialEndsAt);
