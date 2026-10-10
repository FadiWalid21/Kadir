using Kadr.Platform.Infrastructure.Email;
using Kadr.Platform.Infrastructure.Imaging;
using Kadr.Platform.Infrastructure.Storage;
using Kadr.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Platform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IImageProcessor, LogoImageProcessor>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        return services;
    }
}
