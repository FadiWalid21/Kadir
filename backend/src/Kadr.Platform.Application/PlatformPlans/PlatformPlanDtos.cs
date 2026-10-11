using System.Text.Json;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.PlatformPlans;

public sealed record PlatformPlanDto(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    string PricingModel,
    int? EmployeeCap,
    decimal? UnitPrice,
    int? MinBilledEmployees,
    string Currency,
    string FeaturesJson,
    IReadOnlyDictionary<string, bool> Features,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PlatformPlanListResultDto(IReadOnlyList<PlatformPlanDto> Items);

/// <summary>Full replacement of the editable catalog fields. Code and pricing model are never editable.</summary>
public sealed record PlatformUpdatePlanRequest(
    string NameAr,
    string NameEn,
    int? EmployeeCap,
    decimal? UnitPrice,
    int? MinBilledEmployees,
    string FeaturesJson,
    bool IsActive);

public static class PlatformPlanLimits
{
    public const int MaxNameLength = 200;
    public const int MaxEmployeeCap = 1_000_000;
    public const int MaxMinBilledEmployees = 1_000_000;
    public const decimal MaxUnitPrice = 1_000_000_000m;
}

public static class PlatformPlanFeatures
{
    /// <summary>
    /// Accepts a JSON object whose keys are known feature keys and whose values are booleans, and returns it
    /// normalized to every known key (missing keys become false) so stored JSON is always complete.
    /// </summary>
    public static bool TryNormalize(string? json, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var map = FeatureKeys.All.ToDictionary(k => k, _ => false, StringComparer.Ordinal);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!map.ContainsKey(prop.Name)
                    || prop.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return false;
                }

                map[prop.Name] = prop.Value.GetBoolean();
            }

            normalized = JsonSerializer.Serialize(map);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
