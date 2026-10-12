using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Attendance;

public sealed class WorkLocation : AggregateRoot, ITenantOwned, IAuditable
{
    public const int MinRadiusMeters = 50;
    public const int MaxRadiusMeters = 1000;

    private WorkLocation()
    {
    }

    public Guid TenantId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public WorkLocationType Type { get; private set; }
    public Guid? LinkedBranchId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int GeofenceRadiusMeters { get; private set; }
    public string? Address { get; private set; }
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static WorkLocation Create(
        Guid tenantId,
        string nameAr,
        string? nameEn,
        WorkLocationType type,
        Guid? linkedBranchId,
        double latitude,
        double longitude,
        int geofenceRadiusMeters,
        string? address,
        DateOnly? validFrom,
        DateOnly? validTo,
        bool isActive,
        Guid? createdBy)
    {
        var location = new WorkLocation
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
        location.Apply(
            nameAr,
            nameEn,
            type,
            linkedBranchId,
            latitude,
            longitude,
            geofenceRadiusMeters,
            address,
            validFrom,
            validTo,
            isActive);
        return location;
    }

    public void Update(
        string nameAr,
        string? nameEn,
        WorkLocationType type,
        Guid? linkedBranchId,
        double latitude,
        double longitude,
        int geofenceRadiusMeters,
        string? address,
        DateOnly? validFrom,
        DateOnly? validTo,
        bool isActive,
        Guid? updatedBy)
    {
        Apply(
            nameAr,
            nameEn,
            type,
            linkedBranchId,
            latitude,
            longitude,
            geofenceRadiusMeters,
            address,
            validFrom,
            validTo,
            isActive);
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>Usable for a punch on <paramref name="workDate"/>: active and inside its validity window.</summary>
    public bool IsValidOn(DateOnly workDate) =>
        IsActive
        && (ValidFrom is null || ValidFrom <= workDate)
        && (ValidTo is null || ValidTo >= workDate);

    private void Apply(
        string nameAr,
        string? nameEn,
        WorkLocationType type,
        Guid? linkedBranchId,
        double latitude,
        double longitude,
        int geofenceRadiusMeters,
        string? address,
        DateOnly? validFrom,
        DateOnly? validTo,
        bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (type == WorkLocationType.ExternalSite && linkedBranchId is not null)
        {
            throw new ArgumentException("Only branch locations can be linked to a branch.", nameof(linkedBranchId));
        }

        if (linkedBranchId == Guid.Empty)
        {
            throw new ArgumentException("Linked branch id is invalid.", nameof(linkedBranchId));
        }

        if (double.IsNaN(latitude) || double.IsNaN(longitude))
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Coordinates must be numbers.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90);
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180);
        ArgumentOutOfRangeException.ThrowIfLessThan(geofenceRadiusMeters, MinRadiusMeters);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(geofenceRadiusMeters, MaxRadiusMeters);

        if (validFrom is not null && validTo is not null && validTo < validFrom)
        {
            throw new ArgumentException("Valid-to date must be on or after valid-from date.", nameof(validTo));
        }

        NameAr = nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        Type = type;
        LinkedBranchId = linkedBranchId;
        Latitude = latitude;
        Longitude = longitude;
        GeofenceRadiusMeters = geofenceRadiusMeters;
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        ValidFrom = validFrom;
        ValidTo = validTo;
        IsActive = isActive;
    }
}
