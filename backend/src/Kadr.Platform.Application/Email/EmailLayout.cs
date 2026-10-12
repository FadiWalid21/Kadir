using System.Net;
using System.Text;

namespace Kadr.Platform.Application.Email;

/// <summary>
/// Shared HTML shell for transactional mail. Inline styles only (email clients).
/// Brand burgundy is hard-coded hex here — CSS tokens do not apply inside Brevo.
/// </summary>
public static class EmailLayout
{
    public const string BrandHex = "#7A1F3D";
    public const string BrandName = "Kadr";

    public static string Wrap(
        string titleEn,
        string titleAr,
        string bodyHtml,
        string? ctaUrl = null,
        string? ctaLabelEn = null,
        string? ctaLabelAr = null,
        string? footerNoteEn = null,
        string? footerNoteAr = null)
    {
        var sb = new StringBuilder(2048);
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/></head>");
        sb.Append("<body style=\"margin:0;padding:0;background:#F7F4F2;font-family:Arial,Helvetica,sans-serif;color:#1A1215;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#F7F4F2;padding:24px 12px;\">");
        sb.Append("<tr><td align=\"center\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;background:#FFFFFF;border-radius:10px;overflow:hidden;border:1px solid #E8E0DC;\">");

        // Header
        sb.Append("<tr><td style=\"background:")
            .Append(BrandHex)
            .Append(";padding:20px 24px;\">");
        sb.Append("<p style=\"margin:0;font-size:20px;font-weight:700;color:#FFFFFF;letter-spacing:0;\">")
            .Append(HtmlEncode(BrandName))
            .Append("</p>");
        sb.Append("</td></tr>");

        // Titles + body
        sb.Append("<tr><td style=\"padding:28px 24px 8px 24px;\">");
        sb.Append("<h1 style=\"margin:0 0 8px 0;font-size:20px;line-height:1.4;font-weight:700;color:#1A1215;\">")
            .Append(HtmlEncode(titleEn))
            .Append("</h1>");
        sb.Append("<h2 dir=\"rtl\" lang=\"ar\" style=\"margin:0 0 20px 0;font-size:18px;line-height:1.65;font-weight:700;color:#1A1215;\">")
            .Append(HtmlEncode(titleAr))
            .Append("</h2>");
        sb.Append(bodyHtml);
        sb.Append("</td></tr>");

        if (!string.IsNullOrWhiteSpace(ctaUrl)
            && !string.IsNullOrWhiteSpace(ctaLabelEn)
            && !string.IsNullOrWhiteSpace(ctaLabelAr))
        {
            sb.Append("<tr><td style=\"padding:8px 24px 24px 24px;\" align=\"center\">");
            sb.Append("<a href=\"")
                .Append(HtmlEncode(ctaUrl))
                .Append("\" style=\"display:inline-block;background:")
                .Append(BrandHex)
                .Append(";color:#FFFFFF;text-decoration:none;font-size:15px;font-weight:700;padding:12px 22px;border-radius:8px;\">")
                .Append(HtmlEncode(ctaLabelEn))
                .Append(" / ")
                .Append(HtmlEncode(ctaLabelAr))
                .Append("</a>");
            sb.Append("</td></tr>");
        }

        if (!string.IsNullOrWhiteSpace(footerNoteEn) || !string.IsNullOrWhiteSpace(footerNoteAr))
        {
            sb.Append("<tr><td style=\"padding:0 24px 24px 24px;border-top:1px solid #E8E0DC;\">");
            if (!string.IsNullOrWhiteSpace(footerNoteEn))
            {
                sb.Append("<p style=\"margin:16px 0 4px 0;font-size:12px;line-height:1.5;color:#6B5C60;\">")
                    .Append(HtmlEncode(footerNoteEn))
                    .Append("</p>");
            }

            if (!string.IsNullOrWhiteSpace(footerNoteAr))
            {
                sb.Append("<p dir=\"rtl\" lang=\"ar\" style=\"margin:0;font-size:12px;line-height:1.65;color:#6B5C60;\">")
                    .Append(HtmlEncode(footerNoteAr))
                    .Append("</p>");
            }

            sb.Append("</td></tr>");
        }

        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }

    /// <summary>Single-locale body (notifications) inside the same chrome.</summary>
    public static string WrapLocalized(
        string brandLabel,
        string title,
        string bodyPlain,
        bool arabic,
        string? ctaUrl = null,
        string? ctaLabel = null)
    {
        var dir = arabic ? "rtl" : "ltr";
        var lang = arabic ? "ar" : "en";
        var bodyHtml =
            $"<div dir=\"{dir}\" lang=\"{lang}\">"
            + $"<p style=\"margin:0 0 12px 0;font-size:15px;line-height:{(arabic ? "1.65" : "1.5")};color:#1A1215;\">{HtmlEncode(bodyPlain)}</p>"
            + "</div>";

        var sb = new StringBuilder(2048);
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/></head>");
        sb.Append("<body style=\"margin:0;padding:0;background:#F7F4F2;font-family:Arial,Helvetica,sans-serif;color:#1A1215;\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#F7F4F2;padding:24px 12px;\">");
        sb.Append("<tr><td align=\"center\">");
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;background:#FFFFFF;border-radius:10px;overflow:hidden;border:1px solid #E8E0DC;\">");
        sb.Append("<tr><td style=\"background:")
            .Append(BrandHex)
            .Append(";padding:20px 24px;\">");
        sb.Append("<p style=\"margin:0;font-size:20px;font-weight:700;color:#FFFFFF;\">")
            .Append(HtmlEncode(brandLabel))
            .Append("</p></td></tr>");
        sb.Append("<tr><td style=\"padding:28px 24px;\" dir=\"")
            .Append(dir)
            .Append("\" lang=\"")
            .Append(lang)
            .Append("\">");
        sb.Append("<h1 style=\"margin:0 0 16px 0;font-size:20px;line-height:1.4;font-weight:700;color:#1A1215;\">")
            .Append(HtmlEncode(title))
            .Append("</h1>");
        sb.Append(bodyHtml);
        sb.Append("</td></tr>");

        if (!string.IsNullOrWhiteSpace(ctaUrl) && !string.IsNullOrWhiteSpace(ctaLabel))
        {
            sb.Append("<tr><td style=\"padding:0 24px 28px 24px;\" align=\"center\">");
            sb.Append("<a href=\"")
                .Append(HtmlEncode(ctaUrl))
                .Append("\" style=\"display:inline-block;background:")
                .Append(BrandHex)
                .Append(";color:#FFFFFF;text-decoration:none;font-size:15px;font-weight:700;padding:12px 22px;border-radius:8px;\">")
                .Append(HtmlEncode(ctaLabel))
                .Append("</a></td></tr>");
        }

        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }

    public static string ParagraphEn(string text) =>
        $"<p style=\"margin:0 0 12px 0;font-size:15px;line-height:1.5;color:#1A1215;\">{HtmlEncode(text)}</p>";

    public static string ParagraphAr(string text) =>
        $"<p dir=\"rtl\" lang=\"ar\" style=\"margin:0 0 12px 0;font-size:15px;line-height:1.65;color:#1A1215;\">{HtmlEncode(text)}</p>";

    public static string StrongLine(string text) =>
        $"<p style=\"margin:16px 0 8px 0;font-size:15px;font-weight:700;color:#1A1215;\">{HtmlEncode(text)}</p>";

    public static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);
}
