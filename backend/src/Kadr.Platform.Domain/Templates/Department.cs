using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Templates;

public sealed class Department : AggregateRoot, ITenantOwned, IAuditable, ITemplateSourced
{
    private Department()
    {
    }

    public Guid TenantId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string SearchName { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public Guid? ManagerEmployeeId { get; private set; }
    public string? SourceKey { get; private set; }
    public Guid? SourceTemplateId { get; private set; }
    public int SourceVersion { get; private set; }
    public bool IsCustomized { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Department CreateFromTemplate(
        Guid tenantId,
        string sourceKey,
        Guid sourceTemplateId,
        int sourceVersion,
        string nameAr,
        string nameEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        return new Department
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            SearchName = BuildSearchName(nameAr, nameEn),
            SourceKey = sourceKey.Trim(),
            SourceTemplateId = sourceTemplateId,
            SourceVersion = sourceVersion,
            IsCustomized = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static Department CreateCustom(Guid tenantId, string nameAr, string nameEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        return new Department
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            SearchName = BuildSearchName(nameAr, nameEn),
            SourceKey = null,
            SourceTemplateId = null,
            SourceVersion = 0,
            IsCustomized = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Update(string nameAr, string nameEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SearchName = BuildSearchName(nameAr, nameEn);
        IsCustomized = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ResetFromTemplate(string nameAr, string nameEn, int sourceVersion)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SearchName = BuildSearchName(nameAr, nameEn);
        SourceVersion = sourceVersion;
        IsCustomized = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetHierarchy(Guid? parentId, Guid? managerEmployeeId)
    {
        if (parentId == Id)
        {
            throw new InvalidOperationException("org.department_cycle");
        }

        ParentId = parentId;
        ManagerEmployeeId = managerEmployeeId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string BuildSearchName(string nameAr, string nameEn) =>
        Kadr.SharedKernel.Text.ArabicText.NormalizeForSearch($"{nameAr} {nameEn}");
}
