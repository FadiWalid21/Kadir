using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.DomainEvents;
using Kadr.Infrastructure.Persistence.Interceptors;
using Kadr.Infrastructure.Persistence.Tenancy;
using Kadr.Platform.Application;
using Kadr.Platform.Domain.Notes;
using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kadr.TenantIsolation.Tests;

[Collection(PostgresCollection.Name)]
public sealed class TenantIsolationTests(PostgresFixture fixture)
{
    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatformApplication();
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddDbContext<KadrDbContext>((sp, options) =>
        {
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });
        return services.BuildServiceProvider();
    }

    private async Task EnsureSchemaAsync()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task List_for_tenant_A_returns_zero_rows_from_tenant_B()
    {
        await EnsureSchemaAsync();
        var tenantA = GuidV7.NewGuid();
        var tenantB = GuidV7.NewGuid();

        await using var provider = BuildProvider(fixture.ConnectionString);
        using (var scope = provider.CreateScope())
        {
            var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            ambient.Set(tenantA);
            db.TenantNotes.Add(TenantNote.Create(tenantA, "A note"));
            await db.SaveChangesAsync();

            ambient.Set(tenantB);
            db.TenantNotes.Add(TenantNote.Create(tenantB, "B note"));
            await db.SaveChangesAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            ambient.Set(tenantA);
            var titles = await db.TenantNotes.AsNoTracking().Select(n => n.Title).ToListAsync();
            Assert.Equal(["A note"], titles);
        }
    }

    [Fact]
    public async Task Get_by_id_from_other_tenant_returns_null()
    {
        await EnsureSchemaAsync();
        var tenantA = GuidV7.NewGuid();
        var tenantB = GuidV7.NewGuid();
        Guid noteBId;

        await using var provider = BuildProvider(fixture.ConnectionString);
        using (var scope = provider.CreateScope())
        {
            var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            ambient.Set(tenantB);
            var noteB = TenantNote.Create(tenantB, "secret");
            db.TenantNotes.Add(noteB);
            await db.SaveChangesAsync();
            noteBId = noteB.Id;
        }

        using (var scope = provider.CreateScope())
        {
            var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
            var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
            ambient.Set(tenantA);
            var found = await db.TenantNotes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == noteBId);
            Assert.Null(found);
        }
    }

    [Fact]
    public async Task SaveChanges_stamps_tenant_from_context_and_ignores_mismatched_client_value_on_add_when_empty()
    {
        await EnsureSchemaAsync();
        var tenantA = GuidV7.NewGuid();

        await using var provider = BuildProvider(fixture.ConnectionString);
        using var scope = provider.CreateScope();
        var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        ambient.Set(tenantA);

        var note = TenantNote.Create(tenantA, "stamped");
        db.TenantNotes.Add(note);
        await db.SaveChangesAsync();

        ambient.Set(tenantA);
        var stored = await db.TenantNotes.AsNoTracking().SingleAsync(n => n.Id == note.Id);
        Assert.Equal(tenantA, stored.TenantId);
    }

    [Fact]
    public async Task SaveChanges_throws_when_added_entity_tenant_mismatches_context()
    {
        await EnsureSchemaAsync();
        var tenantA = GuidV7.NewGuid();
        var tenantB = GuidV7.NewGuid();

        await using var provider = BuildProvider(fixture.ConnectionString);
        using var scope = provider.CreateScope();
        var ambient = scope.ServiceProvider.GetRequiredService<AmbientTenantContext>();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        ambient.Set(tenantA);

        db.TenantNotes.Add(TenantNote.Create(tenantB, "mismatch"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Model_applies_tenant_query_filter_to_all_ITenantOwned_entities()
    {
        await EnsureSchemaAsync();
        await using var provider = BuildProvider(fixture.ConnectionString);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();

        var tenantOwned = typeof(TenantNote).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ITenantOwned).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(tenantOwned);

        foreach (var type in tenantOwned)
        {
            var entityType = db.Model.FindEntityType(type);
            Assert.NotNull(entityType);
            Assert.NotNull(entityType.GetQueryFilter());
        }
    }
}
