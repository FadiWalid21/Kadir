using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Templates;

public sealed class DocumentType : AggregateRoot, ITenantOwned, IAuditable, ITemplateSourced
{
    private DocumentType()
    {
    }

    public Guid TenantId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string SearchName { get; private set; } = string.Empty;
    public bool HasExpiry { get; private set; }
    public string AlertDaysJson { get; private set; } = "[]";
    public string? SourceKey { get; private set; }
    public Guid? SourceTemplateId { get; private set; }
    public int SourceVersion { get; private set; }
    public bool IsCustomized { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static DocumentType CreateFromTemplate(
        Guid tenantId,
        string sourceKey,
        Guid sourceTemplateId,
        int sourceVersion,
        string nameAr,
        string nameEn,
        bool hasExpiry,
        string alertDaysJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        return new DocumentType
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            SearchName = BuildSearchName(nameAr, nameEn),
            HasExpiry = hasExpiry,
            AlertDaysJson = string.IsNullOrWhiteSpace(alertDaysJson) ? "[]" : alertDaysJson,
            SourceKey = sourceKey.Trim(),
            SourceTemplateId = sourceTemplateId,
            SourceVersion = sourceVersion,
            IsCustomized = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Update(string nameAr, string nameEn, bool hasExpiry, string alertDaysJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SearchName = BuildSearchName(nameAr, nameEn);
        HasExpiry = hasExpiry;
        AlertDaysJson = string.IsNullOrWhiteSpace(alertDaysJson) ? "[]" : alertDaysJson;
        IsCustomized = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ResetFromTemplate(
        string nameAr,
        string nameEn,
        bool hasExpiry,
        string alertDaysJson,
        int sourceVersion)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SearchName = BuildSearchName(nameAr, nameEn);
        HasExpiry = hasExpiry;
        AlertDaysJson = string.IsNullOrWhiteSpace(alertDaysJson) ? "[]" : alertDaysJson;
        SourceVersion = sourceVersion;
        IsCustomized = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string BuildSearchName(string nameAr, string nameEn) =>
        $"{nameAr.Trim()} {nameEn.Trim()}".Trim().ToLowerInvariant();
}
