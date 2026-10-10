using Kadr.Infrastructure.Persistence.DomainEvents;
using Kadr.Platform.Domain.Notes;
using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence;

public sealed class KadrDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public KadrDbContext(
        DbContextOptions<KadrDbContext> options,
        ITenantContext tenantContext,
        IDomainEventDispatcher domainEventDispatcher)
        : base(options)
    {
        _tenantContext = tenantContext;
        _domainEventDispatcher = domainEventDispatcher;
    }

    /// <summary>Exposed for EF global query filters (evaluated per query).</summary>
    public Guid? CurrentTenantId => _tenantContext.TenantId;

    public DbSet<TenantNote> TenantNotes => Set<TenantNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KadrDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(KadrDbContext)
                .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity =>
            CurrentTenantId != null && entity.TenantId == CurrentTenantId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker
            .Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .SelectMany(aggregate =>
            {
                var events = aggregate.DomainEvents.ToList();
                aggregate.ClearDomainEvents();
                return events;
            })
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (domainEvents.Count > 0)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public override int SaveChanges() =>
        SaveChangesAsync().GetAwaiter().GetResult();
}
