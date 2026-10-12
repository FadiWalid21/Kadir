using Kadr.Platform.Application.Email;

namespace Kadr.Platform.Tests.Email;

public sealed class BrevoOptionsTests
{
    [Fact]
    public void Validate_passes_when_unconfigured()
    {
        Assert.Null(BrevoOptions.Validate(new BrevoOptions()));
    }

    [Fact]
    public void Validate_passes_when_key_without_sender_so_logging_fallback_can_run()
    {
        Assert.Null(BrevoOptions.Validate(new BrevoOptions { ApiKey = "x" }));
    }

    [Fact]
    public void CanSend_requires_both()
    {
        Assert.False(new BrevoOptions { ApiKey = "x" }.CanSend);
        Assert.True(new BrevoOptions { ApiKey = "x", SenderEmail = "a@b.com" }.CanSend);
    }

    [Fact]
    public void Validate_rejects_bad_sender_when_can_send()
    {
        var err = BrevoOptions.Validate(new BrevoOptions { ApiKey = "x", SenderEmail = "not-an-email" });
        Assert.NotNull(err);
        Assert.DoesNotContain("x", err, StringComparison.Ordinal);
    }
}
