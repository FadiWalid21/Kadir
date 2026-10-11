using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Domain.Tenants;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Subscriptions;

public sealed class DevSubscriptionService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ISubscriptionCache cache,
    IAuditLogWriter audit) : IDevSubscriptionService
{
    public async Task<Result> ApplyAsync(DevSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure(ErrorCodes.SubscriptionNotFound, ErrorCodes.SubscriptionNotFound);
        }

        var tenantId = tenantContext.TenantId.Value;
        var subscription = await db.Subscriptions
            .SingleOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return Result.Failure(ErrorCodes.SubscriptionNotFound, ErrorCodes.SubscriptionNotFound);
        }

        var oldPlanId = subscription.PlanId;
        string? oldPlanCode = null;
        var planChanged = false;

        if (!string.IsNullOrWhiteSpace(request.PlanCode))
        {
            var plan = await db.Plans.AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.Code == request.PlanCode && p.IsActive,
                    cancellationToken)
                .ConfigureAwait(false);
            if (plan is null)
            {
                return Result.Failure(ErrorCodes.PlanNotFound, ErrorCodes.PlanNotFound);
            }

            oldPlanCode = await db.Plans.AsNoTracking()
                .Where(p => p.Id == oldPlanId)
                .Select(p => p.Code)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (plan.Id != oldPlanId)
            {
                subscription.ChangePlan(plan.Id);
                planChanged = true;
            }
        }

        if (request.ExpireTrial == true)
        {
            subscription.ExpireTrial();
            var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);
            tenant.Suspend();
        }
        else if (request.TrialEndsAt is not null)
        {
            subscription.ExtendTrial(request.TrialEndsAt.Value);
            var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);
            tenant.SetTrialEndsAt(request.TrialEndsAt.Value);
            tenant.ResumeTrial();
        }
        else if (request.ExtendDays is int days && days > 0)
        {
            var baseEnd = subscription.TrialEndsAt ?? subscription.RenewsAt ?? DateTimeOffset.UtcNow;
            if (baseEnd < DateTimeOffset.UtcNow)
            {
                baseEnd = DateTimeOffset.UtcNow;
            }

            var newEnd = baseEnd.AddDays(days);
            subscription.ExtendTrial(newEnd);
            var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantId, cancellationToken)
                .ConfigureAwait(false);
            tenant.SetTrialEndsAt(newEnd);
            tenant.ResumeTrial();
        }

        if (planChanged)
        {
            audit.Stage(
                tenantId,
                tenantContext.UserId,
                "subscription.plan_changed",
                "Subscription",
                subscription.Id,
                JsonSerializer.Serialize(new { planCode = oldPlanCode }),
                JsonSerializer.Serialize(new { planCode = request.PlanCode }));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        cache.Invalidate(tenantId);

        return Result.Success();
    }
}
