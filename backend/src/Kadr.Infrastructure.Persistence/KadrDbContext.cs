using Kadr.Identity.Domain.Roles;
using Kadr.Identity.Domain.Tokens;
using Kadr.Identity.Domain.Users;
using Kadr.Infrastructure.Persistence.DomainEvents;
using Kadr.Platform.Domain.Idempotency;
using Kadr.Platform.Domain.Org;
using Kadr.Platform.Domain.Plans;
using Kadr.Platform.Domain.Settings;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.Platform.Domain.Tenants;
using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence;

public class KadrDbContext : DbContext
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

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KadrDbContext).Assembly);
        OnModelCreatingPartial(modelBuilder);
        ApplyTenantFilters(modelBuilder);
    }

    /// <summary>Hook for test DbContexts to register harness-only entities.</summary>
    protected virtual void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
    }

    protected void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
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
