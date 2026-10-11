namespace Kadr.Api.Middleware;

/// <summary>Ensures API responses are never stored by browsers or shared caches (PWA / ESS-01).</summary>
public sealed class NoStoreCacheMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1"))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
        }

        await next(context).ConfigureAwait(false);
    }
}
