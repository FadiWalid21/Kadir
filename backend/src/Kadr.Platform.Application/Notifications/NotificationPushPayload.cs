using Kadr.Platform.Domain.Notifications;
using Kadr.SharedKernel.Abstractions;

namespace Kadr.Platform.Application.Notifications;

/// <summary>Builds localized, non-sensitive web-push payloads from inbox notifications.</summary>
public static class NotificationPushPayload
{
    public static PushMessage Build(Notification notification, string? preferredLanguage)
    {
        var arabic = !string.Equals(preferredLanguage, "en", StringComparison.OrdinalIgnoreCase);
        var title = arabic ? notification.TitleAr : notification.TitleEn;
        var body = arabic ? notification.BodyAr : notification.BodyEn;
        var link = NotificationLinkPaths.IsAllowed(notification.LinkPath)
            ? notification.LinkPath
            : null;

        return new PushMessage(
            Title: Truncate(title, 64),
            Body: Truncate(body, 120),
            LinkPath: link,
            Tag: notification.Type,
            Lang: arabic ? "ar" : "en",
            Dir: arabic ? "rtl" : "ltr");
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value;
        }

        return value[..(max - 1)] + "…";
    }
}
