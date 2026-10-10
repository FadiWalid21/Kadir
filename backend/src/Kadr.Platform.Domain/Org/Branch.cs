using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Org;

public sealed class Branch : AggregateRoot, ITenantOwned, IAuditable
{
    private Branch()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public int? GeofenceRadiusMeters { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Branch CreateDefault(Guid tenantId, Guid legalEntityId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Branch
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Name = name.Trim(),
            IsDefault = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
