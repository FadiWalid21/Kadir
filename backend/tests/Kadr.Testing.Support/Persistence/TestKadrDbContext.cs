using Kadr.Infrastructure.Persistence;
using Kadr.Infrastructure.Persistence.DomainEvents;
using Kadr.SharedKernel.Tenancy;
using Kadr.Testing.Support.Notes;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Testing.Support.Persistence;

public sealed class TestKadrDbContext : KadrDbContext
{
    public TestKadrDbContext(
        DbContextOptions<KadrDbContext> options,
        ITenantContext tenantContext,
        IDomainEventDispatcher domainEventDispatcher)
        : base(options, tenantContext, domainEventDispatcher)
    {
    }

    public DbSet<TenantNote> TenantNotes => Set<TenantNote>();

    protected override void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantNoteConfiguration());
    }
}
