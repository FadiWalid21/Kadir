using System.Diagnostics;
using Kadr.SharedKernel.Tenancy;
using Serilog.Context;

namespace Kadr.Api.Middleware;

public sealed class LogContextMiddleware(RequestDelegate next)
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var correlationId = context.Request.Headers[CorrelationHeader].FirstOrDefault()
            ?? Activity.Current?.Id
            ?? Guid.NewGuid().ToString("N");

        context.Response.Headers[CorrelationHeader] = correlationId;

        using (LogContext.PushProperty("correlation_id", correlationId))
        using (LogContext.PushProperty("tenant_id", tenantContext.TenantId))
        using (LogContext.PushProperty("user_id", tenantContext.UserId))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
