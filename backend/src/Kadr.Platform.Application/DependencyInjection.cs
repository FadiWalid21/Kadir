using Kadr.Platform.Application.Notes;
using Kadr.Platform.Domain.Notes;
using Kadr.SharedKernel.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Platform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformApplication(this IServiceCollection services)
    {
        services.AddScoped<IDomainEventHandler<TenantNoteCreated>, TenantNoteCreatedHandler>();
        return services;
    }
}
