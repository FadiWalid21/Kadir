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
        RegisterPushSender(services, configuration);

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

    /// <summary>
    /// Logs which IPushNotificationSender was chosen. Call once after Build so incomplete VAPID is obvious.
    /// Never includes key material.
    /// </summary>
    public static void LogWebPushSenderSelection(IServiceProvider services, IConfiguration configuration)
    {
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Kadr.Platform.Infrastructure.Push");
        var sender = services.GetRequiredService<IPushNotificationSender>();
        var typeName = sender.GetType().Name;
        var options = ResolveWebPushOptions(configuration);
        var validationError = WebPushOptions.Validate(options);

        if (typeName == nameof(WebPushNotificationSender))
        {
            logger.LogInformation("Push sender registered: {PushSender}", typeName);
            return;
        }

        if (validationError is not null)
        {
            logger.LogWarning(
                "WebPush configuration is invalid ({Reason}); using {PushSender} (push is logged, not delivered). Fix VAPID settings or set WebPush:Enabled=false.",
                validationError,
                typeName);
            return;
        }

        if (options.Enabled)
        {
            logger.LogWarning(
                "WebPush:Enabled is true but sender is {PushSender}; check VAPID configuration and restart.",
                typeName);
            return;
        }

        logger.LogInformation(
            "Push sender registered: {PushSender} (WebPush disabled; messages are logged, not delivered)",
            typeName);
    }

    private static void RegisterPushSender(IServiceCollection services, IConfiguration configuration)
    {
        var options = ResolveWebPushOptions(configuration);
        var validationError = WebPushOptions.Validate(options);

        // Incomplete VAPID must not take down the host — degrade like Brevo → logging sender.
        if (validationError is null && options.Enabled)
        {
            services.AddHttpClient(nameof(WebPushNotificationSender));
            services.AddSingleton<IPushNotificationSender, WebPushNotificationSender>();
            return;
        }

        services.AddSingleton<IPushNotificationSender, LoggingPushNotificationSender>();
    }

    private static WebPushOptions ResolveWebPushOptions(IConfiguration configuration) =>
        configuration.GetSection(WebPushOptions.SectionName).Get<WebPushOptions>() ?? new WebPushOptions();

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
