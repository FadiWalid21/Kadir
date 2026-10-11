using Kadr.Identity.Application.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Kadr.Infrastructure.Persistence.Auth;

public sealed class PermissionService(KadrDbContext db, IMemoryCache cache) : IPermissionService, IPermissionCacheInvalidator
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey("perms", tenantId, userId);
        if (cache.TryGetValue(key, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var active = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.TenantId == tenantId)
            .Select(u => u.IsActive)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!active)
        {
            IReadOnlyList<string> empty = [];
            cache.Set(key, empty, CacheDuration);
            return empty;
        }

        var permissions = await (
                from ur in db.UserRoles.AsNoTracking()
                join rp in db.RolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
                where ur.UserId == userId && ur.TenantId == tenantId
                select rp.PermissionCode)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        cache.Set(key, (IReadOnlyList<string>)permissions, CacheDuration);
        return permissions;
    }

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey("roles", tenantId, userId);
        if (cache.TryGetValue(key, out IReadOnlyList<string>? cached) && cached is not null)
        {
            return cached;
        }

        var roles = await (
                from ur in db.UserRoles.AsNoTracking()
                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where ur.UserId == userId && ur.TenantId == tenantId
                select r.Name)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        cache.Set(key, (IReadOnlyList<string>)roles, CacheDuration);
        return roles;
    }

    public void Invalidate(Guid tenantId, Guid userId)
    {
        cache.Remove(CacheKey("perms", tenantId, userId));
        cache.Remove(CacheKey("roles", tenantId, userId));
    }

    private static string CacheKey(string kind, Guid tenantId, Guid userId) =>
        $"{kind}:{tenantId:N}:{userId:N}";
}
