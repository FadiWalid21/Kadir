using Kadr.Platform.Application.Email;
using Kadr.Platform.Domain.Notifications;
using Kadr.SharedKernel.Abstractions;

namespace Kadr.Platform.Application.Notifications;

/// <summary>Email subject and body builders per event type, in Arabic or English.</summary>
public static class NotificationEmailTemplates
{
    public static EmailMessage Build(
        string toEmail,
        Notification notification,
        string? preferredLanguage,
        string? appBaseUrl)
    {
        var arabic = !string.Equals(preferredLanguage, "en", StringComparison.OrdinalIgnoreCase);
        var title = arabic ? notification.TitleAr : notification.TitleEn;
        var body = arabic ? notification.BodyAr : notification.BodyEn;
        var brand = arabic ? "كادر" : EmailLayout.BrandName;
        var subject = $"{brand}: {title}";

        var link = BuildLink(appBaseUrl, notification.LinkPath);
        var cta = CallToAction(notification.Type, arabic);
        var html = EmailLayout.WrapLocalized(brand, title, body, arabic, link, link is null ? null : cta);
        var text = link is null ? $"{title}\n{body}" : $"{title}\n{body}\n{cta}: {link}";
        return new EmailMessage(toEmail, subject, html, text);
    }

    private static string CallToAction(string eventType, bool arabic) => eventType switch
    {
        NotificationEventTypes.WorkflowApprovalRequired => arabic ? "مراجعة الطلب" : "Review the request",
        NotificationEventTypes.InvitationAccepted => arabic ? "عرض الدعوات" : "View invitations",
        _ => arabic ? "عرض طلباتي" : "View my requests",
    };

    private static string? BuildLink(string? baseUrl, string? path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }
}
