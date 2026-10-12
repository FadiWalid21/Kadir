namespace Kadr.Platform.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Type,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string? LinkPath,
    string? MetadataJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    bool IsRead);

public sealed record PagedNotificationsDto(
    IReadOnlyList<NotificationDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int UnreadCount);

public sealed record UnreadCountDto(int Count);

public sealed record MarkAllReadResultDto(int Updated);

public sealed record NotificationPreferenceDto(
    string EventType,
    bool InAppEnabled,
    bool EmailEnabled,
    bool PushEnabled);

public sealed record NotificationPreferencesDto(IReadOnlyList<NotificationPreferenceDto> Items);

public sealed record UpdateNotificationPreferencesRequest(IReadOnlyList<NotificationPreferenceDto> Items);
