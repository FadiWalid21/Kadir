using System.Globalization;
using System.Text.Json;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

/// <summary>
/// Copies legacy TenantSettings overtime multipliers into TenantRuleValue (one-way, no dual-read after).
/// </summary>
public static class OvertimeRuleMigration
{
    private static readonly (string SettingKey, string RuleKey)[] Map =
    [
        (SettingKeys.OvertimeMultiplierDay, PayrollRuleKeys.OvertimeMultiplierDay),
        (SettingKeys.OvertimeMultiplierNight, PayrollRuleKeys.OvertimeMultiplierNight),
        (SettingKeys.OvertimeMultiplierHoliday, PayrollRuleKeys.OvertimeMultiplierHoliday),
    ];

    public static async Task CopySettingsIntoRulesAsync(
        KadrDbContext db,
        Guid tenantId,
        Guid legalEntityId,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var settingKeys = Map.Select(m => m.SettingKey).ToArray();
        var settings = await db.TenantSettings
            .Where(s => settingKeys.Contains(s.Key))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var entry in db.ChangeTracker.Entries<Platform.Domain.Settings.TenantSetting>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (!settingKeys.Contains(entry.Entity.Key))
            {
                continue;
            }

            if (settings.All(s => s.Key != entry.Entity.Key))
            {
                settings.Add(entry.Entity);
            }
        }

        var bySetting = settings.ToDictionary(s => s.Key, StringComparer.Ordinal);

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var (settingKey, ruleKey) in Map)
        {
            if (!bySetting.TryGetValue(settingKey, out var setting))
            {
                continue;
            }

            var multiplier = ParseDecimal(setting.ValueJson);
            if (multiplier is null)
            {
                continue;
            }

            var valueJson = JsonSerializer.Serialize(new { multiplier });
            var existing = await db.TenantRuleValues
                .Where(x => x.LegalEntityId == legalEntityId && x.RuleKey == ruleKey)
                .OrderByDescending(x => x.EffectiveFrom)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null
                && string.Equals(existing.ValueJson, valueJson, StringComparison.Ordinal))
            {
                continue;
            }

            db.TenantRuleValues.Add(TenantRuleValue.CreateCustom(
                tenantId,
                legalEntityId,
                ruleKey,
                PayrollRuleValueTypes.Decimal,
                valueJson,
                asOf,
                "Migrated from tenant settings",
                createdBy,
                sourceRuleSetId: null,
                sourceVersion: null,
                isVerified: false));
        }
    }

    public static async Task CopyAllTenantsAsync(KadrDbContext db, CancellationToken cancellationToken = default)
    {
        var entities = await db.LegalEntities.IgnoreQueryFilters()
            .Where(le => le.IsDefault)
            .Select(le => new { le.TenantId, le.Id })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var group in entities.GroupBy(e => e.TenantId))
        {
            foreach (var le in group)
            {
                // Caller must set tenant ambient when required for SaveChanges stamping.
                await CopySettingsIntoRulesAsync(db, le.TenantId, le.Id, null, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static decimal? ParseDecimal(string valueJson)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<decimal>(valueJson);
            return parsed > 0 ? parsed : null;
        }
        catch (JsonException)
        {
            if (decimal.TryParse(
                    valueJson.Trim('"'),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var n)
                && n > 0)
            {
                return n;
            }

            return null;
        }
    }
}
