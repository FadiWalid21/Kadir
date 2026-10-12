using Kadr.Infrastructure.Persistence.Payroll;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Seeding;

public sealed class StatutoryRuleSetSeeder(
    KadrDbContext db,
    ITenantContextAccessor tenantContext,
    ILogger<StatutoryRuleSetSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var defaults = EgyptStatutoryRuleCatalog.CreateDefaults();
        var existingKeys = await db.StatutoryRuleSets
            .AsNoTracking()
            .Where(x => x.Country == EgyptStatutoryRuleCatalog.Country)
            .Select(x => new { x.RuleKey, x.Version })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingSet = existingKeys.Select(x => (x.RuleKey, x.Version)).ToHashSet();

        var added = 0;
        foreach (var row in defaults)
        {
            if (existingSet.Contains((row.RuleKey, row.Version)))
            {
                continue;
            }

            db.StatutoryRuleSets.Add(row);
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Seeded {Count} Egypt statutory payroll rule defaults [VERIFY]", added);
        }

        await BackfillTenantRulesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task BackfillTenantRulesAsync(CancellationToken cancellationToken)
    {
        var tenants = await db.LegalEntities.IgnoreQueryFilters()
            .Where(le => le.IsDefault)
            .Select(le => new { le.TenantId, LegalEntityId = le.Id })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenants.Count == 0)
        {
            return;
        }

        var platform = await db.StatutoryRuleSets
            .AsNoTracking()
            .Where(x => x.Country == EgyptStatutoryRuleCatalog.Country)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existing = await db.TenantRuleValues.IgnoreQueryFilters()
            .AsNoTracking()
            .Select(x => new { x.TenantId, x.LegalEntityId, x.RuleKey })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingSet = existing.Select(x => (x.TenantId, x.LegalEntityId, x.RuleKey)).ToHashSet();

        var added = 0;
        foreach (var group in tenants.GroupBy(t => t.TenantId))
        {
            tenantContext.Set(group.Key);
            try
            {
                var batch = 0;
                foreach (var le in group)
                {
                    foreach (var source in platform)
                    {
                        if (existingSet.Contains((le.TenantId, le.LegalEntityId, source.RuleKey)))
                        {
                            continue;
                        }

                        db.TenantRuleValues.Add(
                            TenantRuleValue.CreateFromPlatform(
                                le.TenantId,
                                le.LegalEntityId,
                                source,
                                createdBy: null));
                        existingSet.Add((le.TenantId, le.LegalEntityId, source.RuleKey));
                        batch++;
                    }
                }

                if (batch > 0)
                {
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    added += batch;
                }
            }
            finally
            {
                tenantContext.Clear();
            }
        }

        if (added > 0)
        {
            logger.LogInformation("Backfilled {Count} tenant payroll rule values from platform defaults", added);
        }

        await MigrateOvertimeSettingsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task MigrateOvertimeSettingsAsync(CancellationToken cancellationToken)
    {
        var tenants = await db.LegalEntities.IgnoreQueryFilters()
            .Where(le => le.IsDefault)
            .Select(le => new { le.TenantId, LegalEntityId = le.Id })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var migrated = 0;
        foreach (var group in tenants.GroupBy(t => t.TenantId))
        {
            tenantContext.Set(group.Key);
            try
            {
                foreach (var le in group)
                {
                    var before = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);
                    await OvertimeRuleMigration
                        .CopySettingsIntoRulesAsync(db, le.TenantId, le.LegalEntityId, null, cancellationToken)
                        .ConfigureAwait(false);
                    var after = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);
                    if (after > before)
                    {
                        migrated += after - before;
                    }
                }

                if (db.ChangeTracker.HasChanges())
                {
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                tenantContext.Clear();
            }
        }

        if (migrated > 0)
        {
            logger.LogInformation("Migrated overtime multipliers from settings into {Count} rule rows", migrated);
        }
    }
}
