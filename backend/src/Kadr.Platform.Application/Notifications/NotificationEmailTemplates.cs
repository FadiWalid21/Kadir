using System.Net;
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
        var subject = $"{(arabic ? "كادر" : "Kadr")}: {title}";

        var link = BuildLink(appBaseUrl, notification.LinkPath);
        var cta = CallToAction(notification.Type, arabic);
        var dir = arabic ? "rtl" : "ltr";
        var lang = arabic ? "ar" : "en";

        var html =
            $"<div dir=\"{dir}\" lang=\"{lang}\">"
            + $"<h2>{WebUtility.HtmlEncode(title)}</h2>"
            + $"<p>{WebUtility.HtmlEncode(body)}</p>"
            + (link is null ? string.Empty : $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(cta)}</a></p>")
            + "</div>";
        var text = link is null ? $"{title}\n{body}" : $"{title}\n{body}\n{cta}: {link}";
        return new EmailMessage(toEmail, subject, html, text);
    }

    private static string CallToAction(string eventType, bool arabic) => eventType switch
    {
        NotificationEventTypes.WorkflowApprovalRequired => arabic ? "مراجعة الطلب" : "Review the request",
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
