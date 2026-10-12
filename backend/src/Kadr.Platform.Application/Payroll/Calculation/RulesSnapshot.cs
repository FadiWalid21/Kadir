using System.Text.Json;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Payroll.Calculation;

/// <summary>
/// Immutable rule values for a payroll run. Values are JSON blobs keyed by <see cref="PayrollRuleKeys"/>.
/// Never hardcode rates in the engine — read them only from this snapshot.
/// </summary>
public sealed class RulesSnapshot
{
    private readonly IReadOnlyDictionary<string, string> _values;

    private RulesSnapshot(IReadOnlyDictionary<string, string> values)
    {
        _values = values;
    }

    public static RulesSnapshot From(IReadOnlyDictionary<string, string> values) =>
        new(new Dictionary<string, string>(values, StringComparer.Ordinal));

    public IReadOnlyDictionary<string, string> Values => _values;

    public decimal GetMoney(string ruleKey) =>
        ReadDecimalProperty(ruleKey, "amount")
        ?? throw new InvalidOperationException($"Rule '{ruleKey}' missing money amount.");

    public decimal GetPercent(string ruleKey) =>
        ReadDecimalProperty(ruleKey, "rate")
        ?? throw new InvalidOperationException($"Rule '{ruleKey}' missing percent rate.");

    public decimal? GetOptionalMultiplier(string ruleKey)
    {
        if (!_values.TryGetValue(ruleKey, out var json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("multiplier", out var el)
                || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            return el.ValueKind == JsonValueKind.Number ? el.GetDecimal() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string GetEnum(string ruleKey)
    {
        if (!_values.TryGetValue(ruleKey, out var json))
        {
            throw new InvalidOperationException($"Rule '{ruleKey}' missing.");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("value", out var el) || el.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException($"Rule '{ruleKey}' missing enum value.");
            }

            return el.GetString() ?? throw new InvalidOperationException($"Rule '{ruleKey}' enum is null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Rule '{ruleKey}' invalid json.", ex);
        }
    }

    public IReadOnlyList<TaxBracket> GetTaxBrackets()
    {
        if (!_values.TryGetValue(PayrollRuleKeys.TaxBrackets, out var json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("brackets", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<TaxBracket>();
            foreach (var band in arr.EnumerateArray())
            {
                var from = band.GetProperty("from").GetDecimal();
                decimal? to = band.TryGetProperty("to", out var toEl) && toEl.ValueKind != JsonValueKind.Null
                    ? toEl.GetDecimal()
                    : null;
                var rate = band.GetProperty("rate").GetDecimal();
                list.Add(new TaxBracket(from, to, rate));
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public RoundingPolicy GetRoundingPolicy()
    {
        var value = GetEnum(PayrollRuleKeys.PayrollRounding);
        // Catalog seed: half_away_from_zero — 2 decimals per modules/07 §2.2
        return value switch
        {
            "half_away_from_zero" => RoundingPolicy.HalfAwayFromZero2,
            _ => RoundingPolicy.HalfAwayFromZero2,
        };
    }

    public string GetTaxMethod() => GetEnum(PayrollRuleKeys.PayrollTaxMethod);

    public string GetWorkingDaysBasis() => GetEnum(PayrollRuleKeys.PayrollWorkingDaysBasis);

    private decimal? ReadDecimalProperty(string ruleKey, string property)
    {
        if (!_values.TryGetValue(ruleKey, out var json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            return el.GetDecimal();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
