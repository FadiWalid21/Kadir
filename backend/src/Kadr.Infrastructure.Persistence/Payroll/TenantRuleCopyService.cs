using Kadr.Platform.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

public static class TenantRuleCopyService
{
    public static async Task CopyPlatformDefaultsAsync(
        KadrDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var platform = await db.StatutoryRuleSets
            .AsNoTracking()
            .Where(x => x.Country == EgyptStatutoryRuleCatalog.Country)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (platform.Count == 0)
        {
            platform = EgyptStatutoryRuleCatalog.CreateDefaults().ToList();
            db.StatutoryRuleSets.AddRange(platform);
        }

        var existingKeys = await db.TenantRuleValues
            .Where(x => x.LegalEntityId == legalEntityId)
            .Select(x => x.RuleKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existing = existingKeys.ToHashSet(StringComparer.Ordinal);

        foreach (var source in platform)
        {
            if (existing.Contains(source.RuleKey))
            {
                continue;
            }

            db.TenantRuleValues.Add(
                TenantRuleValue.CreateFromPlatform(tenantId, legalEntityId, source, createdBy));
        }
    }
}
