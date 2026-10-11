using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Subscriptions;
using Kadr.Platform.Domain.Plans;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Subscriptions;

public sealed class SubscriptionGuard(
    KadrDbContext db,
    ITenantContext tenantContext,
    ISubscriptionCache cache) : ISubscriptionGuard
{
    public async Task<Result> EnsureWritableAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsFailure)
        {
            return Result.Failure(snapshot.ErrorCode!, snapshot.ErrorMessage!);
        }

        return snapshot.Value!.ReadOnly
            ? Result.Failure(ErrorCodes.SubscriptionExpired, ErrorCodes.SubscriptionExpired)
            : Result.Success();
    }

    public async Task<Result> EnsureFeatureAsync(string featureKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureKey) || !FeatureKeys.All.Contains(featureKey))
        {
            return Result.Failure(ErrorCodes.PlanFeatureNotAvailable, ErrorCodes.PlanFeatureNotAvailable);
        }

        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsFailure)
        {
            return Result.Failure(snapshot.ErrorCode!, snapshot.ErrorMessage!);
        }

        if (!snapshot.Value!.Features.TryGetValue(featureKey, out var enabled) || !enabled)
        {
            return Result.Failure(ErrorCodes.PlanFeatureNotAvailable, ErrorCodes.PlanFeatureNotAvailable);
        }

        return Result.Success();
    }

    public async Task<Result> EnsureEmployeeCapacityAsync(
        int requestedActiveCount,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsFailure)
        {
            return Result.Failure(snapshot.ErrorCode!, snapshot.ErrorMessage!);
        }

        var cap = snapshot.Value!.EmployeeCap;
        if (cap is null)
        {
            return Result.Success();
        }

        return requestedActiveCount > cap.Value
            ? Result.Failure(ErrorCodes.PlanEmployeeCapExceeded, ErrorCodes.PlanEmployeeCapExceeded)
            : Result.Success();
    }

    public async Task<Result<SubscriptionSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (tenantContext.TenantId is null)
        {
            return Result.Failure<SubscriptionSnapshot>(
                ErrorCodes.SubscriptionNotFound,
                ErrorCodes.SubscriptionNotFound);
        }

        var tenantId = tenantContext.TenantId.Value;
        if (cache.TryGet(tenantId, out var cached) && cached is not null)
        {
            return Result.Success(cached);
        }

        var row = await (
                from s in db.Subscriptions.AsNoTracking()
                join p in db.Plans.AsNoTracking() on s.PlanId equals p.Id
                where s.TenantId == tenantId
                select new { Subscription = s, Plan = p })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result.Failure<SubscriptionSnapshot>(
                ErrorCodes.SubscriptionNotFound,
                ErrorCodes.SubscriptionNotFound);
        }

        var snapshot = ToSnapshot(row.Subscription, row.Plan, DateTimeOffset.UtcNow);
        cache.Set(tenantId, snapshot);
        return Result.Success(snapshot);
    }

    internal static SubscriptionSnapshot ToSnapshot(Subscription subscription, Plan plan, DateTimeOffset utcNow)
    {
        var trialEnd = subscription.TrialEndsAt ?? subscription.RenewsAt;
        var (state, readOnly, daysLeft) = SubscriptionStateCalculator.Compute(
            subscription.Status,
            trialEnd,
            subscription.CancelledAt,
            utcNow);

        return new SubscriptionSnapshot(
            subscription.Id,
            subscription.TenantId,
            plan.Id,
            plan.Code,
            plan.NameAr,
            plan.NameEn,
            plan.PricingModel.ToString(),
            plan.EmployeeCap,
            plan.UnitPrice,
            plan.MinBilledEmployees,
            plan.Currency,
            subscription.Status.ToString(),
            state,
            readOnly,
            subscription.StartsAt,
            trialEnd,
            subscription.RenewsAt,
            daysLeft,
            PlanFeaturesParser.Parse(plan.FeaturesJson));
    }
}
