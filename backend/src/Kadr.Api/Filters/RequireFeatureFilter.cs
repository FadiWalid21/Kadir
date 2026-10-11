using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Filters;

public sealed class RequireFeatureFilter(string featureKey) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var guard = http.RequestServices.GetRequiredService<ISubscriptionGuard>();
        var localizer = http.RequestServices.GetRequiredService<IErrorLocalizer>();

        var result = await guard.EnsureFeatureAsync(featureKey, http.RequestAborted).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return await next(context).ConfigureAwait(false);
        }

        var code = result.ErrorCode ?? ErrorCodes.PlanFeatureNotAvailable;
        return Results.Problem(
            ApiProblemDetails.Create(http, StatusCodes.Status403Forbidden, code, localizer));
    }
}

public static class RequireFeatureExtensions
{
    public static RouteHandlerBuilder RequireFeature(this RouteHandlerBuilder builder, string featureKey) =>
        builder.AddEndpointFilter(new RequireFeatureFilter(featureKey));
}
