using Kadr.Identity.Application.Auth;
using Kadr.Infrastructure.Persistence;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me", GetMeAsync)
            .RequireAuthorization()
            .WithName("GetMe");
        return app;
    }

    private static async Task<IResult> GetMeAsync(
        ITenantContext tenantContext,
        KadrDbContext db,
        IPermissionService permissions,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Results.Unauthorized();
        }

        var tenantId = tenantContext.TenantId.Value;
        var userId = tenantContext.UserId.Value;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.FullName, u.Email, u.PreferredLanguage })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Id, t.Name, t.Slug, t.LogoFileId })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var roles = await permissions.GetRoleNamesAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var perms = await permissions.GetPermissionsAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);
        var logoUrl = tenant.LogoFileId is null ? null : $"/api/v1/files/{tenant.LogoFileId:D}";

        return Results.Ok(new MeResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.PreferredLanguage,
            roles,
            perms,
            new MeTenant(tenant.Id, tenant.Name, tenant.Slug, logoUrl)));
    }
}

public sealed record MeResponse(
    Guid Id,
    string FullName,
    string Email,
    string Language,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    MeTenant Tenant);

public sealed record MeTenant(Guid Id, string Name, string Slug, string? LogoUrl);
