using Kadr.Identity.Application.Auth;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace Kadr.Api.Auth;

public static class PermissionAuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, PlatformAdminAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            foreach (var code in PermissionCodes.All)
            {
                options.AddPolicy(code, policy =>
                    policy.Requirements.Add(new PermissionRequirement(code)));
            }

            options.AddPolicy(
                AuthorizationPolicies.PlatformAdmin,
                policy => policy.Requirements.Add(new PlatformAdminRequirement()));
        });

        return services;
    }
}

public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

public sealed class PermissionAuthorizationHandler(
    IPermissionService permissionService,
    ITenantContext tenantContext) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return;
        }

        var permissions = await permissionService
            .GetPermissionsAsync(tenantContext.TenantId.Value, tenantContext.UserId.Value)
            .ConfigureAwait(false);

        if (permissions.Contains(requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }
    }
}
