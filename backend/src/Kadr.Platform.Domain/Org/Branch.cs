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
    public OutOfRangePolicy OutOfRangePolicy { get; private set; } = OutOfRangePolicy.Block;
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

    public bool HasLocationConfigured =>
        Latitude is not null
        && Longitude is not null
        && GeofenceRadiusMeters is > 0;

    public void SetLocation(
        double latitude,
        double longitude,
        int radiusMeters,
        OutOfRangePolicy outOfRangePolicy)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90);
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180);
        ArgumentOutOfRangeException.ThrowIfLessThan(radiusMeters, 50);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(radiusMeters, 1000);

        Latitude = latitude;
        Longitude = longitude;
        GeofenceRadiusMeters = radiusMeters;
        OutOfRangePolicy = outOfRangePolicy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        GeofenceRadiusMeters = null;
        OutOfRangePolicy = OutOfRangePolicy.Block;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
