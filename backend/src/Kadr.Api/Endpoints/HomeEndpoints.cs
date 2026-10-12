using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;

namespace Kadr.Api.Endpoints;

public static class HomeEndpoints
{
    public static IEndpointRouteBuilder MapHomeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/home/dashboard", GetDashboardAsync)
            .RequireAuthorization()
            .WithName("HomeDashboard");
        return app;
    }

    private static async Task<IResult> GetDashboardAsync(
        HttpContext httpContext,
        IHomeDashboardService home,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await home.GetDashboardAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var code = result.ErrorCode ?? ErrorCodes.TenantNotFound;
        var status = code == ErrorCodes.TenantNotFound
            ? StatusCodes.Status401Unauthorized
            : StatusCodes.Status400BadRequest;
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
