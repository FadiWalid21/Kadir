using System.Security.Claims;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Kadr.Api.Auth;

public sealed class PlatformAdminRequirement : IAuthorizationRequirement;

public sealed class PlatformAdminAuthorizationHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminRequirement requirement)
    {
        var flag = context.User.FindFirstValue(ClaimNames.PlatformAdmin);
        if (string.Equals(flag, "true", StringComparison.Ordinal)
            && context.User.Identity?.IsAuthenticated == true)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
