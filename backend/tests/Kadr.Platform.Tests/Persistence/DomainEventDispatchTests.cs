using Kadr.Infrastructure.Persistence.DomainEvents;
using Kadr.Infrastructure.Persistence.Interceptors;
using Kadr.Infrastructure.Persistence.Tenancy;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Ids;
using Kadr.Testing.Support.Notes;
using Kadr.Testing.Support.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.Platform.Tests.Persistence;

public sealed class DomainEventDispatchTests
{
    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IDomainEventHandler<TenantNoteCreated>, TenantNoteCreatedHandler>();
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<Kadr.SharedKernel.Tenancy.ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddDbContext<TestKadrDbContext>((sp, options) =>
        {
            options.UseInMemoryDatabase(dbName);
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });
        // Map base options registration used by TestKadrDbContext ctor (DbContextOptions<KadrDbContext>)
        services.AddScoped(sp =>
        {
            var interceptor = sp.GetRequiredService<TenantSaveChangesInterceptor>();
            var builder = new DbContextOptionsBuilder<Kadr.Infrastructure.Persistence.KadrDbContext>();
            builder.UseInMemoryDatabase(dbName);
            builder.AddInterceptors(interceptor);
            return builder.Options;
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SaveChanges_dispatches_domain_events_exactly_once()
    {
        TenantNoteCreatedHandler.ResetHandleCount();

        var tenantId = GuidV7.NewGuid();
        await using var provider = BuildProvider($"events-{Guid.NewGuid():N}");
        using var scope = provider.CreateScope();
        var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
        ambient.Set(tenantId);
        var db = CreateDb(scope);

        db.TenantNotes.Add(TenantNote.Create(tenantId, "Event note"));
        await db.SaveChangesAsync();

        Assert.Equal(1, TenantNoteCreatedHandler.HandleCount);
    }

    [Fact]
    public async Task Failed_save_does_not_dispatch_domain_events()
    {
        TenantNoteCreatedHandler.ResetHandleCount();

        var tenantId = GuidV7.NewGuid();
        await using var provider = BuildProvider($"events-fail-{Guid.NewGuid():N}");
        using var scope = provider.CreateScope();
        var db = CreateDb(scope);
        db.TenantNotes.Add(TenantNote.Create(tenantId, "Should fail"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(0, TenantNoteCreatedHandler.HandleCount);
    }

    private static TestKadrDbContext CreateDb(IServiceScope scope)
    {
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<Kadr.Infrastructure.Persistence.KadrDbContext>>();
        var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        return new TestKadrDbContext(options, ambient, dispatcher);
    }
}
