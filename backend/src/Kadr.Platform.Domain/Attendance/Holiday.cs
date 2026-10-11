using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Attendance;

public sealed class Holiday : AggregateRoot, ITenantOwned, IAuditable
{
    private Holiday()
    {
    }

    public Guid TenantId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public bool IsOfficial { get; private set; }
    public bool RepeatsYearly { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Holiday Create(
        Guid tenantId,
        DateOnly startsOn,
        DateOnly endsOn,
        string nameAr,
        string nameEn,
        bool isOfficial,
        bool repeatsYearly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        if (endsOn < startsOn)
        {
            throw new ArgumentException("End date must be on or after start date.");
        }

        return new Holiday
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            StartsOn = startsOn,
            EndsOn = endsOn,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            IsOfficial = isOfficial,
            RepeatsYearly = repeatsYearly,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Update(
        DateOnly startsOn,
        DateOnly endsOn,
        string nameAr,
        string nameEn,
        bool isOfficial,
        bool repeatsYearly,
        bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        if (endsOn < startsOn)
        {
            throw new ArgumentException("End date must be on or after start date.");
        }

        StartsOn = startsOn;
        EndsOn = endsOn;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        IsOfficial = isOfficial;
        RepeatsYearly = repeatsYearly;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool Covers(DateOnly date)
    {
        if (!IsActive)
        {
            return false;
        }

        if (!RepeatsYearly)
        {
            return date >= StartsOn && date <= EndsOn;
        }

        // Compare month/day within the span in the start year.
        var startMd = StartsOn.Month * 100 + StartsOn.Day;
        var endMd = EndsOn.Month * 100 + EndsOn.Day;
        var dateMd = date.Month * 100 + date.Day;
        if (startMd <= endMd)
        {
            return dateMd >= startMd && dateMd <= endMd;
        }

        // Span crosses year boundary (rare for Egypt civil holidays).
        return dateMd >= startMd || dateMd <= endMd;
    }
}
