using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Kadr.TenantIsolation.Tests.Register;

public sealed class RegisterApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("kadr_register_test")
        .WithUsername("kadr")
        .WithPassword("kadr_test_password")
        .Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _container.GetConnectionString());
        builder.UseSetting("RateLimiting:RegisterPermitLimit", "1000");
        builder.UseEnvironment("Development");
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<PlanSeeder>().SeedAsync();
    }
}
