using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Payroll;

/// <summary>Tenant-owned effective-dated rule value used by payroll calculation.</summary>
public sealed class TenantRuleValue : AggregateRoot, ITenantOwned
{
    private TenantRuleValue()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string RuleKey { get; private set; } = string.Empty;
    public string ValueType { get; private set; } = string.Empty;
    public string ValueJson { get; private set; } = "{}";
    public DateOnly EffectiveFrom { get; private set; }
    public Guid? SourceRuleSetId { get; private set; }
    public int? SourceVersion { get; private set; }
    public bool IsCustomized { get; private set; }
    public bool IsVerified { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static TenantRuleValue CreateFromPlatform(
        Guid tenantId,
        Guid legalEntityId,
        StatutoryRuleSet source,
        Guid? createdBy)
    {
        return new TenantRuleValue
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            RuleKey = source.RuleKey,
            ValueType = source.ValueType,
            ValueJson = source.ValueJson,
            EffectiveFrom = source.EffectiveFrom,
            SourceRuleSetId = source.Id,
            SourceVersion = source.Version,
            IsCustomized = false,
            IsVerified = source.IsVerified,
            Reason = "Copied from platform default",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public static TenantRuleValue CreateCustom(
        Guid tenantId,
        Guid legalEntityId,
        string ruleKey,
        string valueType,
        string valueJson,
        DateOnly effectiveFrom,
        string reason,
        Guid? createdBy,
        Guid? sourceRuleSetId,
        int? sourceVersion,
        bool isVerified)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueType);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new TenantRuleValue
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            RuleKey = ruleKey.Trim(),
            ValueType = valueType.Trim(),
            ValueJson = valueJson,
            EffectiveFrom = effectiveFrom,
            SourceRuleSetId = sourceRuleSetId,
            SourceVersion = sourceVersion,
            IsCustomized = true,
            IsVerified = isVerified,
            Reason = reason.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public static TenantRuleValue CreateReset(
        Guid tenantId,
        Guid legalEntityId,
        StatutoryRuleSet source,
        DateOnly effectiveFrom,
        string reason,
        Guid? createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new TenantRuleValue
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            RuleKey = source.RuleKey,
            ValueType = source.ValueType,
            ValueJson = source.ValueJson,
            EffectiveFrom = effectiveFrom,
            SourceRuleSetId = source.Id,
            SourceVersion = source.Version,
            IsCustomized = false,
            IsVerified = source.IsVerified,
            Reason = reason.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }
}
