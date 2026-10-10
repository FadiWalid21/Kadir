using Kadr.Identity.Application.Security;
using Kadr.SharedKernel.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<DummyPasswordHash>();
        services.AddSingleton<ICommonPasswordList, EmbeddedCommonPasswordList>();
        return services;
    }
}
