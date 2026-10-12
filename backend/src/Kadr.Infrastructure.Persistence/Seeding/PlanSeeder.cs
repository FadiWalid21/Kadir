using System.Text.Json;
using Kadr.Platform.Domain.Plans;
using Kadr.SharedKernel.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Seeding;

public sealed class PlanSeeder(KadrDbContext db, ILogger<PlanSeeder> logger)
{
    // [VERIFY] EGP placeholder monthly prices — not for production billing.
    private const decimal StarterMonthlyPrice = 1499m;
    private const decimal GrowthMonthlyPrice = 3999m;
    private const decimal BusinessMonthlyPrice = 7999m;
    private const decimal CoreUnitPrice = 49m;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await db.Plans.ToListAsync(cancellationToken).ConfigureAwait(false);
        var byCode = existing.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

        UpsertFlat(byCode, PlanCodes.Starter, "المبتدئ", "Starter", 25, StarterMonthlyPrice, FeatureKeys.StarterAndCore);
        UpsertFlat(byCode, PlanCodes.Growth, "النمو", "Growth", 100, GrowthMonthlyPrice, FeatureKeys.Growth);
        UpsertFlat(byCode, PlanCodes.Business, "الأعمال", "Business", 300, BusinessMonthlyPrice, FeatureKeys.Business);
        UpsertPerEmployee(byCode, PlanCodes.Core, "الأساسي", "Core", CoreUnitPrice, 10, FeatureKeys.StarterAndCore);

        // MVP basic reports (RPT-01): enable on existing Starter/Core without wiping admin catalog edits.
        EnsureFeatureEnabled(byCode, PlanCodes.Starter, FeatureKeys.Reports);
        EnsureFeatureEnabled(byCode, PlanCodes.Core, FeatureKeys.Reports);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Upserted platform plans ({Count})", byCode.Count);
    }

    private static void EnsureFeatureEnabled(Dictionary<string, Plan> byCode, string planCode, string featureKey)
    {
        if (!byCode.TryGetValue(planCode, out var plan))
        {
            return;
        }

        Dictionary<string, bool> map;
        try
        {
            map = JsonSerializer.Deserialize<Dictionary<string, bool>>(plan.FeaturesJson)
                  ?? FeatureKeys.All.ToDictionary(k => k, _ => false, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            map = FeatureKeys.All.ToDictionary(k => k, _ => false, StringComparer.Ordinal);
        }

        if (map.TryGetValue(featureKey, out var enabled) && enabled)
        {
            return;
        }

        map[featureKey] = true;
        foreach (var key in FeatureKeys.All)
        {
            map.TryAdd(key, false);
        }

        plan.ApplySeed(
            plan.NameAr,
            plan.NameEn,
            plan.PricingModel,
            plan.EmployeeCap,
            plan.UnitPrice,
            plan.MinBilledEmployees,
            JsonSerializer.Serialize(map),
            plan.IsActive);
    }

    private void UpsertFlat(
        Dictionary<string, Plan> byCode,
        string code,
        string nameAr,
        string nameEn,
        int cap,
        decimal monthlyPrice,
        IReadOnlyList<string> enabledFeatures)
    {
        // Insert-only: platform admins may edit catalog fields; do not overwrite on every boot.
        if (byCode.ContainsKey(code))
        {
            return;
        }

        var json = ToFeaturesJson(enabledFeatures);
        var created = Plan.CreateFlat(code, nameAr, nameEn, cap, monthlyPrice, json);
        db.Plans.Add(created);
        byCode[code] = created;
    }

    private void UpsertPerEmployee(
        Dictionary<string, Plan> byCode,
        string code,
        string nameAr,
        string nameEn,
        decimal unitPrice,
        int minBilled,
        IReadOnlyList<string> enabledFeatures)
    {
        if (byCode.ContainsKey(code))
        {
            return;
        }

        var json = ToFeaturesJson(enabledFeatures);
        var created = Plan.CreatePerEmployee(code, nameAr, nameEn, unitPrice, minBilled, json);
        db.Plans.Add(created);
        byCode[code] = created;
    }

    private static string ToFeaturesJson(IReadOnlyList<string> enabled)
    {
        var map = FeatureKeys.All.ToDictionary(k => k, enabled.Contains, StringComparer.Ordinal);
        return JsonSerializer.Serialize(map);
    }
}
