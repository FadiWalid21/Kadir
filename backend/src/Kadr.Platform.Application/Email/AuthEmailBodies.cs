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
            var html =
                "<p>You are invited to join a company on Kadr.</p>"
                + $"<p><a href=\"{HtmlEncode(acceptUrl)}\">Accept invitation / قبول الدعوة</a></p>"
                + "<p dir=\"rtl\">تمت دعوتك للانضمام إلى شركة على كادر. افتح الرابط لتعيين كلمة المرور (صالح 7 أيام).</p>"
                + $"<p><code>Invitation token: {HtmlEncode(rawTokenFallback)}</code></p>";
            return new EmailMessage(to, subject, html, text);
        }

        var plain = $"Invitation token: {rawTokenFallback}";
        return new EmailMessage(to, subject, plain, plain);
    }

    public static EmailMessage VerifyEmail(string to, string? verifyUrl, string rawTokenFallback)
    {
        var subject = "Verify your Kadr email / تأكيد البريد";
        if (!string.IsNullOrWhiteSpace(verifyUrl))
        {
            var text =
                $"Verify your email:\n{verifyUrl}\n\nتأكيد البريد:\n{verifyUrl}";
            var html =
                $"<p><a href=\"{HtmlEncode(verifyUrl)}\">Verify email / تأكيد البريد</a></p>"
                + $"<p><code>Verify your email with token: {HtmlEncode(rawTokenFallback)}</code></p>";
            return new EmailMessage(to, subject, html, text);
        }

        var plain = $"Verify your email with token: {rawTokenFallback}";
        return new EmailMessage(to, subject, plain, plain);
    }

    public static EmailMessage PasswordReset(
        string to,
        IReadOnlyList<(string CompanyName, string RawToken, string? ResetUrl)> issued)
    {
        var subject = "Reset your Kadr password / إعادة تعيين كلمة المرور";
        var text = new System.Text.StringBuilder();
        text.AppendLine("Use the link for the account you want to recover (valid for 30 minutes).");
        text.AppendLine("استخدم الرابط للحساب الذي تريد استرجاعه (صالح لمدة 30 دقيقة).");
        var html = new System.Text.StringBuilder();
        html.AppendLine("<p>Use the link for the account you want to recover (valid for 30 minutes).</p>");
        html.AppendLine("<p dir=\"rtl\">استخدم الرابط للحساب الذي تريد استرجاعه (صالح لمدة 30 دقيقة).</p>");

        foreach (var (companyName, rawToken, resetUrl) in issued)
        {
            text.AppendLine();
            text.AppendLine(companyName);
            if (!string.IsNullOrWhiteSpace(resetUrl))
            {
                text.AppendLine(resetUrl);
            }

            text.AppendLine($"Reset token: {rawToken}");

            html.AppendLine($"<p><strong>{HtmlEncode(companyName)}</strong></p>");
            if (!string.IsNullOrWhiteSpace(resetUrl))
            {
                html.AppendLine($"<p><a href=\"{HtmlEncode(resetUrl)}\">Reset password / إعادة التعيين</a></p>");
            }

            html.AppendLine($"<p><code>Reset token: {HtmlEncode(rawToken)}</code></p>");
        }

        return new EmailMessage(to, subject, html.ToString(), text.ToString());
    }

    private static string HtmlEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
