using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Payroll;

/// <summary>Platform-owned official default for a payroll rule key (not tenant-scoped).</summary>
public sealed class StatutoryRuleSet : AggregateRoot
{
    private StatutoryRuleSet()
    {
    }

    public string Country { get; private set; } = "EG";
    public string RuleKey { get; private set; } = string.Empty;
    public string ValueType { get; private set; } = string.Empty;
    public string ValueJson { get; private set; } = "{}";
    public DateOnly EffectiveFrom { get; private set; }
    public int Version { get; private set; } = 1;
    public string? SourceNote { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static StatutoryRuleSet Create(
        string country,
        string ruleKey,
        string valueType,
        string valueJson,
        DateOnly effectiveFrom,
        int version,
        string? sourceNote,
        bool isVerified)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueType);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueJson);

        return new StatutoryRuleSet
        {
            Id = GuidV7.NewGuid(),
            Country = country.Trim().ToUpperInvariant(),
            RuleKey = ruleKey.Trim(),
            ValueType = valueType.Trim(),
            ValueJson = valueJson,
            EffectiveFrom = effectiveFrom,
            Version = version,
            SourceNote = string.IsNullOrWhiteSpace(sourceNote) ? null : sourceNote.Trim(),
            IsVerified = isVerified,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
