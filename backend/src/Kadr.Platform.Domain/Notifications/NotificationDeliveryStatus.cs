namespace Kadr.Platform.Domain.Notifications;

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3,
}
