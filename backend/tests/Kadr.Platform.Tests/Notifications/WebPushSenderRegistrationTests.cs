using Kadr.Platform.Infrastructure;
using Kadr.Platform.Infrastructure.Push;
using Kadr.SharedKernel.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Platform.Tests.Notifications;

public sealed class WebPushSenderRegistrationTests
{
    [Fact]
    public void Registers_web_push_sender_when_enabled_and_valid()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebPush:Enabled"] = "true",
                ["WebPush:VapidPublicKey"] = "pub",
                ["WebPush:VapidPrivateKey"] = "priv",
                ["WebPush:Subject"] = "mailto:security@example.com",
            })
            .Build();

        services.AddPlatformInfrastructure(configuration);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPushNotificationSender));
        Assert.Equal(typeof(WebPushNotificationSender), descriptor.ImplementationType);
    }

    [Fact]
    public void Falls_back_to_logging_when_enabled_but_incomplete()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebPush:Enabled"] = "true",
            })
            .Build();

        services.AddPlatformInfrastructure(configuration);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPushNotificationSender));
        Assert.Equal(typeof(LoggingPushNotificationSender), descriptor.ImplementationType);
    }

    [Fact]
    public void Falls_back_to_logging_when_disabled()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebPush:Enabled"] = "false",
            })
            .Build();

        services.AddPlatformInfrastructure(configuration);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPushNotificationSender));
        Assert.Equal(typeof(LoggingPushNotificationSender), descriptor.ImplementationType);
    }
}
