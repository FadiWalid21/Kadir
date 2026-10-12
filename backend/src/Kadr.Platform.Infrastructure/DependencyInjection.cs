using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Infrastructure.Email;
using Kadr.Platform.Infrastructure.Imaging;
using Kadr.Platform.Infrastructure.Push;
using Kadr.Platform.Infrastructure.Storage;
using Kadr.Platform.Infrastructure.Templates;
using Kadr.SharedKernel.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Platform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IImageProcessor, LogoImageProcessor>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddSingleton<IPushEndpointValidator, PushEndpointValidator>();
        services.AddSingleton<IIndustryTemplateSeedSource, IndustryTemplateSeedSource>();

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
}
