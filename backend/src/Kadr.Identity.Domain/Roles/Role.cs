using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Identity.Domain.Roles;

public sealed class Role : AggregateRoot, ITenantOwned, IAuditable
{
    private Role()
    {
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Role CreateSystem(Guid tenantId, string name, string? descriptionAr, string? descriptionEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            IsSystem = true,
            DescriptionAr = descriptionAr,
            DescriptionEn = descriptionEn,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static Role CreateCustom(Guid tenantId, string name, string? descriptionAr, string? descriptionEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            IsSystem = false,
            DescriptionAr = string.IsNullOrWhiteSpace(descriptionAr) ? null : descriptionAr.Trim(),
            DescriptionEn = string.IsNullOrWhiteSpace(descriptionEn) ? null : descriptionEn.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateDetails(string name, string? descriptionAr, string? descriptionEn)
    {
        if (IsSystem)
        {
            throw new InvalidOperationException("System roles cannot be modified.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        DescriptionAr = string.IsNullOrWhiteSpace(descriptionAr) ? null : descriptionAr.Trim();
        DescriptionEn = string.IsNullOrWhiteSpace(descriptionEn) ? null : descriptionEn.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void EnsureCanDelete()
    {
        if (IsSystem)
        {
            throw new InvalidOperationException("System roles cannot be deleted.");
        }
    }
}
