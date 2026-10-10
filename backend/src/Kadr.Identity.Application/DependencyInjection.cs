using Kadr.Identity.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<ICommonPasswordList, EmbeddedCommonPasswordList>();
        return services;
    }
}
