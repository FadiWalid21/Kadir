using Kadr.Platform.Infrastructure;
using Kadr.Platform.Infrastructure.Email;
using Kadr.SharedKernel.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kadr.Platform.Tests.Email;

public sealed class EmailSenderRegistrationTests
{
    [Fact]
    public void Registers_Brevo_when_api_key_and_sender_email_are_set()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.Configure<Kadr.Platform.Application.Email.BrevoOptions>(o =>
        {
            o.ApiKey = "test-key";
            o.SenderEmail = "sender@example.com";
            o.SenderName = "Kadr";
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brevo:ApiKey"] = "test-key",
                ["Brevo:SenderEmail"] = "sender@example.com",
                ["Brevo:SenderName"] = "Kadr",
            })
            .Build();

        services.AddPlatformInfrastructure(configuration);

        using var sp = services.BuildServiceProvider();
        Assert.IsType<BrevoEmailSender>(sp.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void Falls_back_to_logging_when_only_legacy_api_key_is_set()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BREVO_API_KEY"] = "legacy-key",
            })
            .Build();

        services.AddPlatformInfrastructure(configuration);

        using var sp = services.BuildServiceProvider();
        Assert.IsType<LoggingEmailSender>(sp.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void Accepts_legacy_BREVO_API_KEY_with_sender_email()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.Configure<Kadr.Platform.Application.Email.BrevoOptions>(o =>
        {
            o.ApiKey = "legacy-key";
            o.SenderEmail = "sender@example.com";
            o.SenderName = "Kadr";
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BREVO_API_KEY"] = "legacy-key",
                ["Brevo:SenderEmail"] = "sender@example.com",
            })
            .Build();

        services.AddPlatformInfrastructure(configuration);

        using var sp = services.BuildServiceProvider();
        Assert.IsType<BrevoEmailSender>(sp.GetRequiredService<IEmailSender>());
    }
}
