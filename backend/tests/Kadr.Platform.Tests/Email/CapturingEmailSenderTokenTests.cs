using Kadr.SharedKernel.Abstractions;
using Kadr.Testing.Support.Email;

namespace Kadr.Platform.Tests.Email;

public sealed class CapturingEmailSenderTokenTests
{
    [Fact]
    public async Task ExtractLastPasswordResetToken_reads_token_from_html_code_wrapper()
    {
        var sender = new CapturingEmailSender();
        const string token = "AbCdEfGhIjKlMnOpQrStUvWxYz0123456789+/==";
        await sender.SendAsync(new EmailMessage(
            "user@example.com",
            "Reset",
            $"<p><code>Reset token: {token}</code></p>",
            $"Reset token: {token}"));

        Assert.Equal(token, sender.ExtractLastPasswordResetToken());
    }

    [Fact]
    public async Task ExtractLastPasswordResetToken_prefers_url_query_token()
    {
        var sender = new CapturingEmailSender();
        const string token = "url-token-value-abcdefghijklmnopqrstuvwxyz";
        var url = $"https://app.test/reset-password?token={Uri.EscapeDataString(token)}";
        await sender.SendAsync(new EmailMessage(
            "user@example.com",
            "Reset",
            $"<p><a href=\"{url}\">Reset</a></p><p><code>Reset token: {token}</code></p>",
            $"{url}\nReset token: {token}"));

        Assert.Equal(token, sender.ExtractLastPasswordResetToken());
    }
}
