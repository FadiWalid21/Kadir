using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Notifications;

public sealed class NotificationPreference : Entity, ITenantOwned
{
    public const bool DefaultInAppEnabled = true;
    public const bool DefaultEmailEnabled = true;
    public const bool DefaultPushEnabled = false;

    private NotificationPreference()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public bool InAppEnabled { get; private set; } = DefaultInAppEnabled;
    public bool EmailEnabled { get; private set; } = DefaultEmailEnabled;
    public bool PushEnabled { get; private set; } = DefaultPushEnabled;
    public DateTimeOffset UpdatedAt { get; private set; }

    public static NotificationPreference Create(
        Guid tenantId,
        Guid userId,
        string eventType,
        bool inAppEnabled,
        bool emailEnabled,
        bool pushEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        return new NotificationPreference
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            EventType = eventType.Trim(),
            InAppEnabled = inAppEnabled,
            EmailEnabled = emailEnabled,
            PushEnabled = pushEnabled,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Update(bool inAppEnabled, bool emailEnabled, bool pushEnabled)
    {
        InAppEnabled = inAppEnabled;
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
