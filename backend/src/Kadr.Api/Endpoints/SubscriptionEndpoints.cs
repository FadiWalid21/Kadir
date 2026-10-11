using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;

namespace Kadr.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/subscription", GetSubscriptionAsync)
            .RequireAuthorization(PermissionCodes.SubscriptionRead)
            .WithName("GetSubscription");

        app.MapGet("/api/v1/plans", ListPlansAsync)
            .RequireAuthorization(PermissionCodes.SubscriptionRead)
            .WithName("ListPlans");

        return app;
    }

    private static async Task<IResult> GetSubscriptionAsync(
        HttpContext httpContext,
        ISubscriptionService subscriptions,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await subscriptions.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ListPlansAsync(
        HttpContext httpContext,
        ISubscriptionService subscriptions,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await subscriptions.ListPlansAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static IResult ToResult<T>(HttpContext httpContext, Result<T> result, IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var code = result.ErrorCode ?? ErrorCodes.SubscriptionNotFound;
        var status = code switch
        {
            ErrorCodes.SubscriptionNotFound or ErrorCodes.PlanNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.SubscriptionExpired
                or ErrorCodes.PlanFeatureNotAvailable
                or ErrorCodes.PlanEmployeeCapExceeded => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
