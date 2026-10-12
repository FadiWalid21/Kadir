namespace Kadr.Platform.Application.Notifications;

public sealed record VapidPublicKeyDto(string PublicKey);

public sealed record PushKeysDto(string P256dh, string Auth);

public sealed record UpsertPushSubscriptionRequest(
    string Endpoint,
    PushKeysDto Keys,
    string? DeviceLabel = null);

public sealed record PushSubscriptionDeviceDto(
    Guid Id,
    string? DeviceLabel,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    bool IsActive);

public sealed record PushSubscriptionListDto(IReadOnlyList<PushSubscriptionDeviceDto> Items);
