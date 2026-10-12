using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Templates;

public sealed class LeaveType : AggregateRoot, ITenantOwned, IAuditable, ITemplateSourced
{
    private LeaveType()
    {
    }

    public Guid TenantId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string SearchName { get; private set; } = string.Empty;
    public bool Paid { get; private set; }
    public int? DaysPerYear { get; private set; }
    public bool RequiresApproval { get; private set; }
    public bool RequiresAttachment { get; private set; }
    public string? ExtrasJson { get; private set; }
    public string? SourceKey { get; private set; }
    public Guid? SourceTemplateId { get; private set; }
    public int SourceVersion { get; private set; }
    public bool IsCustomized { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static LeaveType CreateFromTemplate(
        Guid tenantId,
        string sourceKey,
        Guid sourceTemplateId,
        int sourceVersion,
        string nameAr,
        string nameEn,
        bool paid,
        int? daysPerYear,
        bool requiresApproval,
        bool requiresAttachment,
        string? extrasJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        return new LeaveType
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            SearchName = BuildSearchName(nameAr, nameEn),
            Paid = paid,
            DaysPerYear = daysPerYear,
            RequiresApproval = requiresApproval,
            RequiresAttachment = requiresAttachment,
            ExtrasJson = string.IsNullOrWhiteSpace(extrasJson) ? null : extrasJson,
            SourceKey = sourceKey.Trim(),
            SourceTemplateId = sourceTemplateId,
            SourceVersion = sourceVersion,
            IsCustomized = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static LeaveType CreateCustom(
        Guid tenantId,
        string nameAr,
        string nameEn,
        bool paid,
        int? daysPerYear,
        bool requiresApproval,
        bool requiresAttachment,
        string? extrasJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        return new LeaveType
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            SearchName = BuildSearchName(nameAr, nameEn),
            Paid = paid,
            DaysPerYear = daysPerYear,
            RequiresApproval = requiresApproval,
            RequiresAttachment = requiresAttachment,
            ExtrasJson = string.IsNullOrWhiteSpace(extrasJson) ? null : extrasJson,
            SourceKey = null,
            SourceTemplateId = null,
            SourceVersion = 0,
            IsCustomized = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Update(
        string nameAr,
        string nameEn,
        bool paid,
        int? daysPerYear,
        bool requiresApproval,
        bool requiresAttachment,
        string? extrasJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SearchName = BuildSearchName(nameAr, nameEn);
        Paid = paid;
        DaysPerYear = daysPerYear;
        RequiresApproval = requiresApproval;
        RequiresAttachment = requiresAttachment;
        ExtrasJson = string.IsNullOrWhiteSpace(extrasJson) ? null : extrasJson;
        IsCustomized = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ResetFromTemplate(
        string nameAr,
        string nameEn,
        bool paid,
        int? daysPerYear,
        bool requiresApproval,
        bool requiresAttachment,
        string? extrasJson,
        int sourceVersion)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SearchName = BuildSearchName(nameAr, nameEn);
        Paid = paid;
        DaysPerYear = daysPerYear;
        RequiresApproval = requiresApproval;
        RequiresAttachment = requiresAttachment;
        ExtrasJson = string.IsNullOrWhiteSpace(extrasJson) ? null : extrasJson;
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
        Kadr.SharedKernel.Text.ArabicText.NormalizeForSearch($"{nameAr} {nameEn}");
}
