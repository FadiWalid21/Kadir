using Kadr.Platform.Application.Email;

namespace Kadr.Platform.Tests.Email;

public sealed class PublicWebLinksTests
{
    [Fact]
    public void TryBuild_returns_null_when_base_missing()
    {
        Assert.Null(PublicWebLinks.TryBuild(null, PublicWebLinks.AcceptInvitationPath, "tok"));
        Assert.Null(PublicWebLinks.TryBuild("  ", PublicWebLinks.AcceptInvitationPath, "tok"));
    }

    [Fact]
    public void TryBuild_normalizes_slash_and_encodes_token()
    {
        var url = PublicWebLinks.TryBuild(
            "https://kadir-teal.vercel.app/",
            PublicWebLinks.AcceptInvitationPath,
            "a+b/c");

        Assert.Equal(
            "https://kadir-teal.vercel.app/accept-invitation?token=a%2Bb%2Fc",
            url);
    }

    [Fact]
    public void Invitation_body_includes_link_without_raw_token_in_html()
    {
        var msg = AuthEmailBodies.Invitation(
            "a@b.com",
            "https://app.example/accept-invitation?token=raw",
            "raw");

        Assert.Contains("accept-invitation?token=raw", msg.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Invitation token: raw", msg.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("accept-invitation?token=raw", msg.TextBody!, StringComparison.Ordinal);
    }
}
