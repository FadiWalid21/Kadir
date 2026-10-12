using System.Text;
using Kadr.SharedKernel.Abstractions;

namespace Kadr.Platform.Application.Email;

public static class AuthEmailBodies
{
    public static EmailMessage Invitation(string to, string? acceptUrl, string rawTokenFallback)
    {
        var subject = "You're invited to Kadr / دعوة للانضمام إلى كادر";
        if (!string.IsNullOrWhiteSpace(acceptUrl))
        {
            var text =
                $"You are invited to join a company on Kadr.\nOpen this link to set your password (expires in 7 days):\n{acceptUrl}\n\n"
                + $"تمت دعوتك للانضمام إلى شركة على كادر.\nافتح الرابط لتعيين كلمة المرور (صالح 7 أيام):\n{acceptUrl}";
            var body =
                EmailLayout.ParagraphEn("You are invited to join a company on Kadr. Open the button below to set your password.")
                + EmailLayout.ParagraphAr("تمت دعوتك للانضمام إلى شركة على كادر. افتح الزر أدناه لتعيين كلمة المرور.");
            var html = EmailLayout.Wrap(
                titleEn: "You're invited",
                titleAr: "دعوة للانضمام",
                bodyHtml: body,
                ctaUrl: acceptUrl,
                ctaLabelEn: "Accept invitation",
                ctaLabelAr: "قبول الدعوة",
                footerNoteEn: "This link expires in 7 days. If you did not expect this email, you can ignore it.",
                footerNoteAr: "ينتهي صلاحية هذا الرابط خلال 7 أيام. إذا لم تتوقع هذه الرسالة، يمكنك تجاهلها.");
            return new EmailMessage(to, subject, html, text);
        }

        var plain = $"Invitation token: {rawTokenFallback}";
        return new EmailMessage(to, subject, EmailLayout.HtmlEncode(plain), plain);
    }

    public static EmailMessage VerifyEmail(string to, string? verifyUrl, string rawTokenFallback)
    {
        var subject = "Verify your Kadr email / تأكيد البريد";
        if (!string.IsNullOrWhiteSpace(verifyUrl))
        {
            var text =
                $"Verify your email:\n{verifyUrl}\n\nتأكيد البريد:\n{verifyUrl}";
            var body =
                EmailLayout.ParagraphEn("Confirm your email address to finish setting up your Kadr account.")
                + EmailLayout.ParagraphAr("أكد بريدك الإلكتروني لإكمال إعداد حسابك في كادر.");
            var html = EmailLayout.Wrap(
                titleEn: "Verify your email",
                titleAr: "تأكيد البريد",
                bodyHtml: body,
                ctaUrl: verifyUrl,
                ctaLabelEn: "Verify email",
                ctaLabelAr: "تأكيد البريد",
                footerNoteEn: "If you did not create an account, you can ignore this email.",
                footerNoteAr: "إذا لم تنشئ حساباً، يمكنك تجاهل هذه الرسالة.");
            return new EmailMessage(to, subject, html, text);
        }

        var plain = $"Verify your email with token: {rawTokenFallback}";
        return new EmailMessage(to, subject, EmailLayout.HtmlEncode(plain), plain);
    }

    public static EmailMessage PasswordReset(
        string to,
        IReadOnlyList<(string CompanyName, string RawToken, string? ResetUrl)> issued)
    {
        var subject = "Reset your Kadr password / إعادة تعيين كلمة المرور";
        var text = new StringBuilder();
        text.AppendLine("Use the link for the account you want to recover (valid for 30 minutes).");
        text.AppendLine("استخدم الرابط للحساب الذي تريد استرجاعه (صالح لمدة 30 دقيقة).");

        var body = new StringBuilder();
        body.Append(EmailLayout.ParagraphEn("Use the button for the account you want to recover."));
        body.Append(EmailLayout.ParagraphAr("استخدم الزر للحساب الذي تريد استرجاعه."));

        foreach (var (companyName, rawToken, resetUrl) in issued)
        {
            text.AppendLine();
            text.AppendLine(companyName);
            if (!string.IsNullOrWhiteSpace(resetUrl))
            {
                text.AppendLine(resetUrl);
            }

            // Token stays in text for ops / test extractors; never in HTML when a URL exists.
            text.AppendLine($"Reset token: {rawToken}");

            body.Append(EmailLayout.StrongLine(companyName));
            if (!string.IsNullOrWhiteSpace(resetUrl))
            {
                body.Append("<p style=\"margin:0 0 16px 0;\">")
                    .Append("<a href=\"")
                    .Append(EmailLayout.HtmlEncode(resetUrl))
                    .Append("\" style=\"color:")
                    .Append(EmailLayout.BrandHex)
                    .Append(";font-weight:700;text-decoration:underline;\">")
                    .Append(EmailLayout.HtmlEncode("Reset password / إعادة التعيين"))
                    .Append("</a></p>");
            }
            else
            {
                body.Append(EmailLayout.ParagraphEn($"Reset token: {rawToken}"));
            }
        }

        var html = EmailLayout.Wrap(
            titleEn: "Reset your password",
            titleAr: "إعادة تعيين كلمة المرور",
            bodyHtml: body.ToString(),
            footerNoteEn: "Each link is valid for 30 minutes. If you did not request a reset, you can ignore this email.",
            footerNoteAr: "كل رابط صالح لمدة 30 دقيقة. إذا لم تطلب إعادة التعيين، يمكنك تجاهل هذه الرسالة.");

        return new EmailMessage(to, subject, html, text.ToString());
    }

    public static EmailMessage SecurityNotice(string to, string subject, string bodyEn, string bodyAr)
    {
        var text = $"{bodyEn}\n{bodyAr}";
        var html = EmailLayout.Wrap(
            titleEn: "Security notice",
            titleAr: "تنبيه أمني",
            bodyHtml: EmailLayout.ParagraphEn(bodyEn) + EmailLayout.ParagraphAr(bodyAr),
            footerNoteEn: "If this was not you, reset your password immediately.",
            footerNoteAr: "إذا لم تكن أنت، فأعد تعيين كلمة المرور فوراً.");
        return new EmailMessage(to, subject, html, text);
    }
}
