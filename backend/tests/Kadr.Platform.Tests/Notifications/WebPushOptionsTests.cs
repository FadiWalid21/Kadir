using Kadr.Platform.Application.Notifications;

namespace Kadr.Platform.Tests.Notifications;

public sealed class WebPushOptionsTests
{
    [Fact]
    public void Disabled_without_keys_is_valid()
    {
        Assert.Null(WebPushOptions.Validate(new WebPushOptions { Enabled = false }));
    }

    [Fact]
    public void Enabled_requires_keys_and_subject_without_leaking_values()
    {
        var error = WebPushOptions.Validate(new WebPushOptions { Enabled = true });
        Assert.NotNull(error);
        Assert.DoesNotContain("BEGIN", error, StringComparison.OrdinalIgnoreCase);

        error = WebPushOptions.Validate(new WebPushOptions
        {
            Enabled = true,
            VapidPublicKey = "pub",
            VapidPrivateKey = "priv",
            Subject = "not-a-uri",
        });
        Assert.NotNull(error);
        Assert.Contains("Subject", error, StringComparison.Ordinal);

        Assert.Null(WebPushOptions.Validate(new WebPushOptions
        {
            Enabled = true,
            VapidPublicKey = "pub",
            VapidPrivateKey = "priv",
            Subject = "mailto:security@example.com",
        }));
    }
}
