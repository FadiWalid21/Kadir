using Kadr.Platform.Application.Email;
using Kadr.Testing.Support.Email;

namespace Kadr.Platform.Tests.Email;

public sealed class AuthEmailBodiesTests
{
    [Fact]
    public void Invitation_with_url_has_cta_and_no_raw_token_in_html()
    {
        const string token = "invite-secret-token-xyz";
        const string url = "https://app.test/accept-invitation?token=invite-secret-token-xyz";
        var message = AuthEmailBodies.Invitation("a@b.com", url, token);

        Assert.Contains(url, message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(EmailLayout.BrandHex, message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain($"Invitation token: {token}", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(url, message.TextBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasswordReset_keeps_token_in_text_not_html_when_url_present()
    {
        const string token = "AbCdEfGhIjKlMnOpQrStUvWxYz0123456789+/==";
        var url = $"https://app.test/reset-password?token={Uri.EscapeDataString(token)}";
        var message = AuthEmailBodies.PasswordReset(
            "a@b.com",
            [("Acme Co", token, url)]);

        Assert.Contains(url, message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain($"Reset token: {token}", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains($"Reset token: {token}", message.TextBody!, StringComparison.Ordinal);

        var capturer = new CapturingEmailSender();
        await capturer.SendAsync(message);
        Assert.Equal(token, capturer.ExtractLastPasswordResetToken());
    }

    [Fact]
    public void VerifyEmail_with_url_has_cta_without_legacy_token_line_in_html()
    {
        const string token = "verify-token-abc";
        const string url = "https://app.test/verify-email?token=verify-token-abc";
        var message = AuthEmailBodies.VerifyEmail("a@b.com", url, token);

        Assert.Contains(url, message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain($"Verify your email with token: {token}", message.HtmlBody, StringComparison.Ordinal);
    }
}
