using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;

namespace Kadr.Api.Middleware;

/// <summary>
/// Sets ambient tenant/user from JWT claims only. Never reads client tenantId body/header/query.
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? context.User.FindFirstValue(ClaimNames.Subject)
                ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            var isPlatformAdmin = string.Equals(
                context.User.FindFirstValue(ClaimNames.PlatformAdmin),
                "true",
                StringComparison.Ordinal);

            if (isPlatformAdmin && Guid.TryParse(subject, out var platformAdminId))
            {
                tenantContext.SetPlatformAdmin(platformAdminId);
            }
            else
            {
                var tenantClaim = context.User.FindFirstValue(ClaimNames.TenantId);
                if (Guid.TryParse(tenantClaim, out var tenantId) && Guid.TryParse(subject, out var userId))
                {
                    tenantContext.Set(tenantId, userId);
                }
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
