using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.DomainEvents;
using Kadr.Infrastructure.Persistence.Interceptors;
using Kadr.Infrastructure.Persistence.Tenancy;
using Kadr.Platform.Application;
using Kadr.Platform.Application.Notes;
using Kadr.Platform.Domain.Notes;
using Kadr.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kadr.Platform.Tests.Persistence;

public sealed class DomainEventDispatchTests
{
    [Fact]
    public async Task SaveChanges_dispatches_domain_events_exactly_once()
    {
        TenantNoteCreatedHandler.ResetHandleCount();

        var tenantId = GuidV7.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatformApplication();
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<Kadr.SharedKernel.Tenancy.ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddDbContext<KadrDbContext>((sp, options) =>
        {
            options.UseInMemoryDatabase($"events-{Guid.NewGuid():N}");
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
        ambient.Set(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();

        db.TenantNotes.Add(TenantNote.Create(tenantId, "Event note"));
        await db.SaveChangesAsync();

        Assert.Equal(1, TenantNoteCreatedHandler.HandleCount);
    }

    [Fact]
    public async Task Failed_save_does_not_dispatch_domain_events()
    {
        TenantNoteCreatedHandler.ResetHandleCount();

        var tenantId = GuidV7.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatformApplication();
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<Kadr.SharedKernel.Tenancy.ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddDbContext<KadrDbContext>((sp, options) =>
        {
            options.UseInMemoryDatabase($"events-fail-{Guid.NewGuid():N}");
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        // No tenant set → interceptor throws before save completes successfully
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        db.TenantNotes.Add(TenantNote.Create(tenantId, "Should fail"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(0, TenantNoteCreatedHandler.HandleCount);
    }
}
