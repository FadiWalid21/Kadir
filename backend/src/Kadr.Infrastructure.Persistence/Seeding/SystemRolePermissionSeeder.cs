using Kadr.Identity.Domain.Roles;
using Kadr.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Seeding;

public sealed class SystemRolePermissionSeeder(KadrDbContext db, ILogger<SystemRolePermissionSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var systemRoles = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.IsSystem)
            .Select(r => new { r.Id, r.TenantId, r.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (systemRoles.Count == 0)
        {
            return;
        }

        var existing = await db.RolePermissions.IgnoreQueryFilters()
            .Select(rp => new { rp.RoleId, rp.PermissionCode })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingSet = existing.Select(x => (x.RoleId, x.PermissionCode)).ToHashSet();

        var added = 0;
        foreach (var role in systemRoles)
        {
            IReadOnlyList<string> desired;
            try
            {
                desired = SystemRolePermissions.ForRole(role.Name);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            foreach (var code in desired)
            {
                if (existingSet.Contains((role.Id, code)))
                {
                    continue;
                }

                db.RolePermissions.Add(RolePermission.Create(role.TenantId, role.Id, code));
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Synced {Count} missing system role permissions", added);
        }
    }
}
