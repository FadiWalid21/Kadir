using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.PlatformTenants;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class PlatformTenantsEndpoints
{
    public static IEndpointRouteBuilder MapPlatformTenantsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/platform/tenants")
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin);

        group.MapGet("/", ListAsync).WithName("PlatformListTenants");
        group.MapGet("/{id:guid}", GetAsync).WithName("PlatformGetTenant");
        group.MapGet("/{id:guid}/audit-summary", AuditSummaryAsync).WithName("PlatformTenantAuditSummary");
        group.MapPost("/{id:guid}/change-plan", ChangePlanAsync).WithName("PlatformChangePlan");
        group.MapPost("/{id:guid}/extend-trial", ExtendTrialAsync).WithName("PlatformExtendTrial");
        group.MapPost("/{id:guid}/suspend", SuspendAsync).WithName("PlatformSuspendTenant");
        group.MapPost("/{id:guid}/resume", ResumeAsync).WithName("PlatformResumeTenant");
        group.MapPost("/{id:guid}/cancel-subscription", CancelSubscriptionAsync)
            .WithName("PlatformCancelSubscription");
        group.MapPost("/{id:guid}/change-industry", ChangeIndustryAsync).WithName("PlatformChangeIndustry");

        return app;
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? plan,
        [FromQuery] string? industry,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        HttpContext httpContext,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await queries
            .ListAsync(
                new PlatformTenantListQuery(q, status, plan, industry, page ?? 1, pageSize ?? 0),
                cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> AuditSummaryAsync(
        Guid id,
        HttpContext httpContext,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetAuditSummaryAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ChangePlanAsync(
        Guid id,
        [FromBody] PlatformChangePlanRequest request,
        HttpContext httpContext,
        IValidator<PlatformChangePlanRequest> validator,
        IPlatformTenantCommands commands,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var result = await commands.ChangePlanAsync(id, request, cancellationToken).ConfigureAwait(false);
        return await ToDetailAsync(id, result, httpContext, queries, localizer, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ExtendTrialAsync(
        Guid id,
        [FromBody] PlatformExtendTrialRequest request,
        HttpContext httpContext,
        IValidator<PlatformExtendTrialRequest> validator,
        IPlatformTenantCommands commands,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var result = await commands.ExtendTrialAsync(id, request, cancellationToken).ConfigureAwait(false);
        return await ToDetailAsync(id, result, httpContext, queries, localizer, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> SuspendAsync(
        Guid id,
        [FromBody] PlatformReasonRequest request,
        HttpContext httpContext,
        IValidator<PlatformReasonRequest> validator,
        IPlatformTenantCommands commands,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var result = await commands.SuspendAsync(id, request, cancellationToken).ConfigureAwait(false);
        return await ToDetailAsync(id, result, httpContext, queries, localizer, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ResumeAsync(
        Guid id,
        [FromBody] PlatformReasonRequest request,
        HttpContext httpContext,
        IValidator<PlatformReasonRequest> validator,
        IPlatformTenantCommands commands,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var result = await commands.ResumeAsync(id, request, cancellationToken).ConfigureAwait(false);
        return await ToDetailAsync(id, result, httpContext, queries, localizer, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> CancelSubscriptionAsync(
        Guid id,
        [FromBody] PlatformReasonRequest request,
        HttpContext httpContext,
        IValidator<PlatformReasonRequest> validator,
        IPlatformTenantCommands commands,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var result = await commands.CancelSubscriptionAsync(id, request, cancellationToken).ConfigureAwait(false);
        return await ToDetailAsync(id, result, httpContext, queries, localizer, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> ChangeIndustryAsync(
        Guid id,
        [FromBody] PlatformChangeIndustryRequest request,
        HttpContext httpContext,
        IValidator<PlatformChangeIndustryRequest> validator,
        IPlatformTenantCommands commands,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (await ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var result = await commands.ChangeIndustryAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return ToProblem(httpContext, result.ErrorCode, localizer);
        }

        var detail = await queries.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (detail.IsFailure)
        {
            return ToProblem(httpContext, detail.ErrorCode, localizer);
        }

        var outcome = result.Value!;
        return Results.Ok(new PlatformChangeIndustryResultDto(
            outcome.OldIndustryCode,
            outcome.NewIndustryCode,
            outcome.Mode,
            outcome.AddedCount,
            outcome.DeactivatedCount,
            detail.Value!));
    }

    private static async Task<IResult?> ValidateAsync<T>(
        T request,
        IValidator<T> validator,
        HttpContext httpContext,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ToProblem(httpContext, ErrorCodes.PlatformValidationFailed, localizer);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (validation.IsValid)
        {
            return null;
        }

        var code = validation.Errors
            .Select(e => e.ErrorCode)
            .FirstOrDefault(c => c is not null
                                 && (c.StartsWith("platform.", StringComparison.Ordinal) || c == ErrorCodes.IndustryInvalid))
            ?? ErrorCodes.PlatformValidationFailed;
        return ToProblem(httpContext, code, localizer);
    }

    private static async Task<IResult> ToDetailAsync(
        Guid id,
        Result mutation,
        HttpContext httpContext,
        IPlatformTenantQueries queries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (mutation.IsFailure)
        {
            return ToProblem(httpContext, mutation.ErrorCode, localizer);
        }

        var detail = await queries.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, detail, localizer);
    }

    private static IResult ToResult<T>(HttpContext httpContext, Result<T> result, IErrorLocalizer localizer) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblem(httpContext, result.ErrorCode, localizer);

    private static IResult ToProblem(HttpContext httpContext, string? code, IErrorLocalizer localizer)
    {
        var errorCode = code ?? ErrorCodes.PlatformValidationFailed;
        var status = errorCode switch
        {
            ErrorCodes.TenantNotFound or ErrorCodes.SubscriptionNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.PlatformTenantStateInvalid or ErrorCodes.PlatformNoChange => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, errorCode, localizer));
    }
}
