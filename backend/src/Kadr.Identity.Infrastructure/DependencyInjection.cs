using Kadr.Identity.Application.Auth;
using Kadr.Identity.Infrastructure.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        return services;
    }
}
