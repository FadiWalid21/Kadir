using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Notifications;

public sealed class PushSubscription : Entity, ITenantOwned
{
    public const int MaxEndpointLength = 2048;
    public const int MaxKeyLength = 512;
    public const int MaxDeviceLabelLength = 128;
    public const int MaxUserAgentLength = 512;
    public const int MaxPerUser = 10;

    private PushSubscription()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public string P256dh { get; private set; } = string.Empty;
    public string Auth { get; private set; } = string.Empty;
    public string? DeviceLabel { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static PushSubscription Create(
        Guid tenantId,
        Guid userId,
        string endpoint,
        string p256dh,
        string auth,
        string? deviceLabel,
        string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(p256dh);
        ArgumentException.ThrowIfNullOrWhiteSpace(auth);

        var now = DateTimeOffset.UtcNow;
        return new PushSubscription
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Endpoint = endpoint.Trim(),
            P256dh = p256dh.Trim(),
            Auth = auth.Trim(),
            DeviceLabel = TrimOrNull(deviceLabel, MaxDeviceLabelLength),
            UserAgent = TrimOrNull(userAgent, MaxUserAgentLength),
            CreatedAt = now,
            LastSeenAt = now,
            IsActive = true,
        };
    }

    public void Refresh(string p256dh, string auth, string? deviceLabel, string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(p256dh);
        ArgumentException.ThrowIfNullOrWhiteSpace(auth);
        P256dh = p256dh.Trim();
        Auth = auth.Trim();
        if (deviceLabel is not null)
        {
            DeviceLabel = TrimOrNull(deviceLabel, MaxDeviceLabelLength);
        }

        if (userAgent is not null)
        {
            UserAgent = TrimOrNull(userAgent, MaxUserAgentLength);
        }

        LastSeenAt = DateTimeOffset.UtcNow;
        IsActive = true;
    }

    public void TouchSeen()
    {
        LastSeenAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        LastSeenAt = DateTimeOffset.UtcNow;
    }

    private static string? TrimOrNull(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
