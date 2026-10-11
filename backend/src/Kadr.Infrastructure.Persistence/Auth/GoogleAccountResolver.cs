using Kadr.Identity.Application.Auth;
using Kadr.Platform.Application.Abstractions;
using Kadr.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Auth;

/// <summary>
/// Cross-tenant Google candidate lookup. Sanctioned IgnoreQueryFilters escape hatch for IAM-07.
/// </summary>
public sealed class GoogleAccountResolver(KadrDbContext db) : IGoogleAccountResolver
{
    public async Task<GoogleAccountResolution> ResolveAsync(
        string providerSubject,
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        var bySubject = await (
                from login in db.ExternalLogins.IgnoreQueryFilters().AsNoTracking()
                join user in db.Users.IgnoreQueryFilters().AsNoTracking() on login.UserId equals user.Id
                join tenant in db.Tenants.AsNoTracking() on user.TenantId equals tenant.Id
                where login.Provider == ExternalLoginProviders.Google
                      && login.ProviderSubject == providerSubject
                select new { user, tenant.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byEmail = await (
                from user in db.Users.IgnoreQueryFilters().AsNoTracking()
                join tenant in db.Tenants.AsNoTracking() on user.TenantId equals tenant.Id
                where user.NormalizedEmail == normalizedEmail
                select new { user, tenant.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var map = new Dictionary<Guid, (Guid TenantId, Guid UserId, string TenantName, DateTimeOffset? LastLoginAt, bool IsActive, bool EmailConfirmed, bool LinkedBySubject)>();

        foreach (var row in bySubject)
        {
            map[row.user.Id] = (
                row.user.TenantId,
                row.user.Id,
                row.Name,
                row.user.LastLoginAt,
                row.user.IsActive,
                row.user.EmailConfirmed,
                true);
        }

        foreach (var row in byEmail)
        {
            if (map.TryGetValue(row.user.Id, out var existing))
            {
                map[row.user.Id] = existing with { EmailConfirmed = row.user.EmailConfirmed };
            }
            else
            {
                map[row.user.Id] = (
                    row.user.TenantId,
                    row.user.Id,
                    row.Name,
                    row.user.LastLoginAt,
                    row.user.IsActive,
                    row.user.EmailConfirmed,
                    false);
            }
        }

        if (map.Count == 0)
        {
            return new GoogleAccountResolution([], false, false);
        }

        var userIds = map.Keys.ToList();
        var roles = await (
                from ur in db.UserRoles.IgnoreQueryFilters().AsNoTracking()
                join role in db.Roles.IgnoreQueryFilters().AsNoTracking() on ur.RoleId equals role.Id
                where userIds.Contains(ur.UserId)
                select new { ur.UserId, role.Name, role.IsSystem })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var roleByUser = roles
            .GroupBy(r => r.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsSystem).ThenBy(x => x.Name).First().Name);

        var candidates = map.Values
            .Select(v => new GoogleCandidate(
                v.TenantId,
                v.UserId,
                v.TenantName,
                roleByUser.GetValueOrDefault(v.UserId, SystemRoleNames.Employee),
                v.LastLoginAt,
                v.IsActive,
                v.EmailConfirmed,
                v.LinkedBySubject))
            .ToList();

        var activeConfirmedOrLinked = candidates
            .Where(c => c.IsActive && (c.LinkedBySubject || c.EmailConfirmed))
            .ToList();

        var hasUnconfirmed = candidates.Any(c =>
            c.IsActive && !c.LinkedBySubject && !c.EmailConfirmed);
        var hasOnlyInactive = candidates.Count > 0
            && candidates.All(c => !c.IsActive)
            && activeConfirmedOrLinked.Count == 0;

        // Unconfirmed email-only matches must not be candidates, but block auto-link.
        if (activeConfirmedOrLinked.Count == 0 && hasUnconfirmed)
        {
            return new GoogleAccountResolution([], true, false);
        }

        return new GoogleAccountResolution(
            activeConfirmedOrLinked,
            hasUnconfirmed && activeConfirmedOrLinked.Count == 0,
            hasOnlyInactive);
    }
}
