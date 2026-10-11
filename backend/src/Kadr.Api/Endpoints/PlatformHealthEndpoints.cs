using Kadr.Platform.Application.Abstractions;
using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Endpoints;

public static class PlatformHealthEndpoints
{
    public static IEndpointRouteBuilder MapPlatformHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/platform/health", GetAsync)
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
            .WithName("PlatformHealth");

        return app;
    }

    private static async Task<IResult> GetAsync(
        IPlatformHealthService health,
        CancellationToken cancellationToken) =>
        Results.Ok(await health.CheckAsync(cancellationToken).ConfigureAwait(false));
}
