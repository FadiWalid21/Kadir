using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Infrastructure.Push;
using Kadr.SharedKernel.Constants;
using Microsoft.Extensions.Options;

namespace Kadr.Platform.Tests.Notifications;

public sealed class PushEndpointValidatorTests
{
    private static PushEndpointValidator Create(params string[] allowedHosts) =>
        new(Options.Create(new WebPushOptions { AllowedEndpointHosts = allowedHosts }));

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/xyz")]
    [InlineData("https://web.push.apple.com/QAbc")]
    [InlineData("https://wns2-bn1p.notify.windows.com/w/?token=1")]
    public void Accepts_known_push_hosts(string endpoint)
    {
        var result = Create().Validate(endpoint);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://evil.example.com/push")]
    [InlineData("https://127.0.0.1/push")]
    [InlineData("https://localhost/push")]
    [InlineData("https://10.0.0.5/push")]
    [InlineData("https://192.168.1.1/push")]
    [InlineData("https://169.254.1.1/push")]
    [InlineData("https://[::1]/push")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void Rejects_unsafe_or_unknown_endpoints(string endpoint)
    {
        var result = Create().Validate(endpoint);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotificationsPushEndpointRejected, result.ErrorCode);
    }

    [Fact]
    public void Honors_custom_allow_list()
    {
        var validator = Create("push.example.test");
        Assert.True(validator.Validate("https://push.example.test/v1/sub").IsSuccess);
        Assert.False(validator.Validate("https://fcm.googleapis.com/fcm/send/x").IsSuccess);
    }
}

public sealed class NotificationLinkPathsTests
{
    [Theory]
    [InlineData("/leave/approvals", true)]
    [InlineData("/leave/me", true)]
    [InlineData("/attendance/corrections", true)]
    [InlineData("/notifications", true)]
    [InlineData("/", true)]
    [InlineData("https://evil.test/x", false)]
    [InlineData("//evil.test", false)]
    [InlineData("/leave/../admin", false)]
    [InlineData("/unknown", false)]
    [InlineData(null, false)]
    public void IsAllowed_matrix(string? path, bool expected)
    {
        Assert.Equal(expected, Kadr.Platform.Domain.Notifications.NotificationLinkPaths.IsAllowed(path));
    }
}
