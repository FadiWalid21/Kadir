using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kadr.Infrastructure.Persistence.Interceptors;

public sealed class TenantSaveChangesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyTenantRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyTenantRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyTenantRules(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (!tenantContext.HasTenant || tenantContext.TenantId is null)
            {
                throw new InvalidOperationException(
                    "Cannot save tenant-owned entities without a tenant in ITenantContext.");
            }

            var currentTenant = tenantContext.TenantId.Value;
            var property = entry.Property(nameof(ITenantOwned.TenantId));

            if (entry.State == EntityState.Added)
            {
                if (property.CurrentValue is Guid existing && existing != Guid.Empty && existing != currentTenant)
                {
                    throw new InvalidOperationException(
                        "TenantId on the entity does not match ITenantContext.");
                }

                property.CurrentValue = currentTenant;
            }
            else if (property.CurrentValue is Guid modified && modified != currentTenant)
            {
                throw new InvalidOperationException(
                    "Cannot change TenantId to a different tenant.");
            }
        }
    }
}
