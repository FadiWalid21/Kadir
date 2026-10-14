using Kadr.Platform.Application.Abstractions;
using Kadr.SharedKernel.Results;

namespace Kadr.Api.Endpoints;

public static class PublicPlansEndpoints
{
    public static IEndpointRouteBuilder MapPublicPlansEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/public/plans", ListPublicPlansAsync)
            .AllowAnonymous()
            .RequireRateLimiting("public-plans")
            .WithName("ListPublicPlans");

        return app;
    }

    private static async Task<IResult> ListPublicPlansAsync(
        ISubscriptionService subscriptions,
        CancellationToken cancellationToken)
    {
        var result = await subscriptions.ListPlansAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(result.Value);
    }
}
