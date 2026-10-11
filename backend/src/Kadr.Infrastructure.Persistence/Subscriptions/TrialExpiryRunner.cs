using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Domain.Audit;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Subscriptions;

public sealed class TrialExpiryRunner(IServiceScopeFactory scopeFactory, ILogger<TrialExpiryRunner> logger)
    : ITrialExpiryRunner
{
    private const int BatchSize = 100;
    private const string AuditAction = "subscription.trial_expired";

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ISubscriptionCache>();
        var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

        var now = DateTimeOffset.UtcNow;
        var processed = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await db.Subscriptions
                .IgnoreQueryFilters()
                .Where(s => s.Status == SubscriptionStatus.Trialing
                    && (s.TrialEndsAt ?? s.RenewsAt) != null
                    && (s.TrialEndsAt ?? s.RenewsAt)! <= now)
                .OrderBy(s => s.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (batch.Count == 0)
            {
                break;
            }

            var tenantIds = batch.Select(s => s.TenantId).Distinct().ToList();
            var tenants = await db.Tenants
                .IgnoreQueryFilters()
                .Where(t => tenantIds.Contains(t.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var tenantsById = tenants.ToDictionary(t => t.Id);

            var subscriptionIds = batch.Select(s => s.Id).ToList();
            var alreadyAudited = await db.AuditLogs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => a.Action == AuditAction && a.EntityId != null && subscriptionIds.Contains(a.EntityId.Value))
                .Select(a => a.EntityId!.Value)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var auditedSet = alreadyAudited.ToHashSet();

            // ITenantOwned SaveChanges requires ambient tenant — one SaveChanges per tenant (not per row).
            foreach (var group in batch.GroupBy(s => s.TenantId))
            {
                tenantAccessor.Set(group.Key);
                try
                {
                    foreach (var subscription in group)
                    {
                        subscription.ExpireTrial();
                        cache.Invalidate(subscription.TenantId);

                        if (tenantsById.TryGetValue(subscription.TenantId, out var tenant))
                        {
                            tenant.Suspend();
                        }

                        if (!auditedSet.Contains(subscription.Id))
                        {
                            db.AuditLogs.Add(AuditLog.Create(
                                subscription.TenantId,
                                null,
                                AuditAction,
                                "Subscription",
                                subscription.Id,
                                null,
                                """{"status":"Suspended"}"""));
                        }

                        logger.LogInformation(
                            "Expired trial for tenant {TenantId} subscription {SubscriptionId}",
                            subscription.TenantId,
                            subscription.Id);
                    }

                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    tenantAccessor.Clear();
                }
            }

            processed += batch.Count;
        }

        if (processed > 0)
        {
            logger.LogInformation("Trial expiry job processed {Count} subscriptions", processed);
        }
    }
}
