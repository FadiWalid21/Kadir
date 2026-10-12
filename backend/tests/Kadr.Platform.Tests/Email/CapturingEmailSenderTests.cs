using Kadr.SharedKernel.Abstractions;
using Kadr.Testing.Support.Email;

namespace Kadr.Platform.Tests.Email;

public sealed class CapturingEmailSenderTests
{
    [Fact]
    public async Task ExtractLastInvitationToken_prefers_url_over_legacy()
    {
        var sender = new CapturingEmailSender();
        await sender.SendAsync(
            new EmailMessage(
                "a@b.com",
                "invite",
                "<a href=\"https://x/accept-invitation?token=from-url\">x</a><code>Invitation token: legacy</code>",
                "https://x/accept-invitation?token=from-url"));

        Assert.Equal("from-url", sender.ExtractLastInvitationToken());
    }

    [Fact]
    public async Task ExtractLastVerificationToken_reads_url()
    {
        var sender = new CapturingEmailSender();
        await sender.SendAsync(
            new EmailMessage(
                "a@b.com",
                "verify",
                "https://x/verify-email?token=ver%2B1",
                null));

        Assert.Equal("ver+1", sender.ExtractLastVerificationToken());
    }
}
