using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;
using Kadr.SharedKernel.Text;

namespace Kadr.Platform.Domain.Payroll;

public sealed class PayComponent : AggregateRoot, ITenantOwned, IAuditable
{
    public const string BasicKey = "basic";

    private PayComponent()
    {
    }

    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string SearchName { get; private set; } = string.Empty;
    public PayComponentKind Kind { get; private set; }
    public PayComponentCalcType CalcType { get; private set; }
    public bool IsTaxable { get; private set; }
    public bool IsInsurable { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? SourceKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static PayComponent Create(
        Guid tenantId,
        string key,
        string nameAr,
        string nameEn,
        PayComponentKind kind,
        PayComponentCalcType calcType,
        bool isTaxable,
        bool isInsurable,
        string? sourceKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        var normalizedKey = key.Trim().ToLowerInvariant();
        return new PayComponent
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            Key = normalizedKey,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            SearchName = BuildSearchName(nameAr, nameEn),
            Kind = kind,
            CalcType = calcType,
            IsTaxable = isTaxable,
            IsInsurable = isInsurable,
            IsActive = true,
            SourceKey = string.IsNullOrWhiteSpace(sourceKey) ? null : sourceKey.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static PayComponent CreateBasic(Guid tenantId) =>
        Create(
            tenantId,
            BasicKey,
            "الأساسي",
            "Basic",
            PayComponentKind.Earning,
            PayComponentCalcType.Fixed,
            isTaxable: true,
            isInsurable: true,
            sourceKey: BasicKey);

    public void Update(
        string nameAr,
        string nameEn,
        PayComponentKind kind,
        PayComponentCalcType calcType,
        bool isTaxable,
        bool isInsurable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SearchName = BuildSearchName(nameAr, nameEn);
        Kind = kind;
        CalcType = calcType;
        IsTaxable = isTaxable;
        IsInsurable = isInsurable;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string BuildSearchName(string nameAr, string nameEn) =>
        ArabicText.NormalizeForSearch($"{nameAr} {nameEn}");
}
