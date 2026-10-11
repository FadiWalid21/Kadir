using System.Text.Json;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Subscriptions;

public static class PlanFeaturesParser
{
    public static IReadOnlyDictionary<string, bool> Parse(string? featuresJson)
    {
        var result = FeatureKeys.All.ToDictionary(k => k, _ => false, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(featuresJson))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(featuresJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (result.ContainsKey(prop.Name) && prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    result[prop.Name] = prop.Value.GetBoolean();
                }
            }
        }
        catch (JsonException)
        {
            // Treat malformed JSON as all features off.
        }

        return result;
    }
}
