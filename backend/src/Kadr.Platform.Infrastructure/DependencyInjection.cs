using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Email;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Infrastructure.Email;
using Kadr.Platform.Infrastructure.Imaging;
using Kadr.Platform.Infrastructure.Push;
using Kadr.Platform.Infrastructure.Storage;
using Kadr.Platform.Infrastructure.Templates;
using Kadr.SharedKernel.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kadr.Platform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IImageProcessor, LogoImageProcessor>();
        services.AddSingleton<IPushEndpointValidator, PushEndpointValidator>();
        services.AddSingleton<IIndustryTemplateSeedSource, IndustryTemplateSeedSource>();

        RegisterEmailSender(services, configuration);

        var webPushEnabled = configuration.GetValue("WebPush:Enabled", false);
        if (webPushEnabled)
        {
            services.AddHttpClient(nameof(WebPushNotificationSender));
            services.AddSingleton<IPushNotificationSender, WebPushNotificationSender>();
        }
        else
        {
            services.AddSingleton<IPushNotificationSender, LoggingPushNotificationSender>();
        }

        return services;
    }

    /// <summary>
    /// Logs which IEmailSender was chosen. Call once after Build so incomplete Brevo config is obvious.
    /// </summary>
    public static void LogEmailSenderSelection(IServiceProvider services, IConfiguration configuration)
    {
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Kadr.Platform.Infrastructure.Email");
        var sender = services.GetRequiredService<IEmailSender>();
        var typeName = sender.GetType().Name;

        if (typeName == nameof(BrevoEmailSender))
        {
            logger.LogInformation("Email sender registered: {EmailSender}", typeName);
            return;
        }

        var hasApiKey = !string.IsNullOrWhiteSpace(ResolveBrevoApiKey(configuration));
        var hasSenderEmail = !string.IsNullOrWhiteSpace(ResolveBrevoSenderEmail(configuration));

        if (hasApiKey && !hasSenderEmail)
        {
            logger.LogWarning(
                "Brevo API key is set but Brevo:SenderEmail is missing; using {EmailSender} (messages are logged, not delivered). Set Brevo:SenderEmail (or BREVO_SENDER_EMAIL) to a verified sender and restart.",
                typeName);
            return;
        }

        logger.LogInformation(
            "Email sender registered: {EmailSender} (Brevo not fully configured; messages are logged, not delivered)",
            typeName);
    }

    private static void RegisterEmailSender(IServiceCollection services, IConfiguration configuration)
    {
        var apiKey = ResolveBrevoApiKey(configuration);
        var senderEmail = ResolveBrevoSenderEmail(configuration);
        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(senderEmail))
        {
            services.AddHttpClient(BrevoEmailSender.HttpClientName);
            services.AddSingleton<IEmailSender, BrevoEmailSender>();
            return;
        }

        services.AddSingleton<IEmailSender, LoggingEmailSender>();
    }

    private static string? ResolveBrevoApiKey(IConfiguration configuration)
    {
        var apiKey = configuration["Brevo:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        // Legacy flat env / user-secret name. Prefer Brevo:ApiKey / Brevo__ApiKey.
        return configuration["BREVO_API_KEY"];
    }

    private static string? ResolveBrevoSenderEmail(IConfiguration configuration)
    {
        var sender = configuration["Brevo:SenderEmail"];
        if (!string.IsNullOrWhiteSpace(sender))
        {
            return sender;
        }

        return configuration["BREVO_SENDER_EMAIL"];
    }
}
