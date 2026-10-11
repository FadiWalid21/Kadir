using System.Text.Json;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;

namespace Kadr.Api.Middleware;

public sealed class SubscriptionReadOnlyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISubscriptionGuard guard,
        ITenantContext tenantContext,
        IErrorLocalizer localizer)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method)
            || SubscriptionWriteAllowList.IsAllowed(context.Request.Path.Value)
            || !tenantContext.HasTenant
            || tenantContext.TenantId is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var result = await guard.EnsureWritableAsync(context.RequestAborted).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var code = result.ErrorCode ?? ErrorCodes.SubscriptionExpired;
        var acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        var payload = new Dictionary<string, object?>
        {
            ["type"] = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            ["title"] = "Forbidden",
            ["status"] = StatusCodes.Status403Forbidden,
            ["detail"] = localizer.GetDetail(code, acceptLanguage),
            ["instance"] = context.Request.Path.Value,
            ["code"] = code,
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload), context.RequestAborted)
            .ConfigureAwait(false);
    }
}
