using FluentValidation;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.PlatformPlans;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class PlatformPlansEndpoints
{
    public static IEndpointRouteBuilder MapPlatformPlansEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/platform/plans")
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin);

        group.MapGet("/", ListAsync).WithName("PlatformListPlans");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("PlatformUpdatePlan");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        IPlatformPlanService plans,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await plans.ListAsync(cancellationToken).ConfigureAwait(false);
        return PlatformEndpointResults.ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] PlatformUpdatePlanRequest request,
        HttpContext httpContext,
        IValidator<PlatformUpdatePlanRequest> validator,
        IPlatformPlanService plans,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (await PlatformEndpointResults.ValidateAsync(request, validator, httpContext, localizer, cancellationToken)
                .ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var result = await plans.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return PlatformEndpointResults.ToResult(httpContext, result, localizer);
    }
}
