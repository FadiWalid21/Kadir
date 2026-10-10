using Kadr.Infrastructure.Persistence.DomainEvents;
using Kadr.Infrastructure.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kadr.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KadrDbContext>
{
    public KadrDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KadrDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=kadr;Username=kadr;Password=design-time")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new KadrDbContext(
            options,
            new AmbientTenantContext(),
            new DomainEventDispatcher(new EmptyServiceProvider()));
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
