using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Plans;

public sealed class Plan : AggregateRoot, IPlatformEntity, IAuditable
{
    private Plan()
    {
    }

    public string Code { get; private set; } = string.Empty;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public PricingModel PricingModel { get; private set; }
    public int? EmployeeCap { get; private set; }
    public decimal? UnitPrice { get; private set; }
    public int? MinBilledEmployees { get; private set; }
    public string Currency { get; private set; } = "EGP";
    public string FeaturesJson { get; private set; } = "{}";
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Plan CreateFlat(
        string code,
        string nameAr,
        string nameEn,
        int employeeCap,
        decimal monthlyPrice,
        string featuresJson)
    {
        return new Plan
        {
            Id = GuidV7.NewGuid(),
            Code = code,
            NameAr = nameAr,
            NameEn = nameEn,
            PricingModel = PricingModel.FlatTier,
            EmployeeCap = employeeCap,
            UnitPrice = monthlyPrice,
            Currency = "EGP",
            FeaturesJson = featuresJson,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static Plan CreatePerEmployee(
        string code,
        string nameAr,
        string nameEn,
        decimal unitPrice,
        int minBilledEmployees,
        string featuresJson)
    {
        return new Plan
        {
            Id = GuidV7.NewGuid(),
            Code = code,
            NameAr = nameAr,
            NameEn = nameEn,
            PricingModel = PricingModel.PerEmployee,
            UnitPrice = unitPrice,
            MinBilledEmployees = minBilledEmployees,
            Currency = "EGP",
            FeaturesJson = featuresJson,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void ApplySeed(
        string nameAr,
        string nameEn,
        PricingModel pricingModel,
        int? employeeCap,
        decimal? unitPrice,
        int? minBilledEmployees,
        string featuresJson,
        bool isActive = true)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        PricingModel = pricingModel;
        EmployeeCap = employeeCap;
        UnitPrice = unitPrice;
        MinBilledEmployees = minBilledEmployees;
        FeaturesJson = featuresJson;
        IsActive = isActive;
        Currency = "EGP";
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
