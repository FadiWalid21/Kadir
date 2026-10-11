using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Subscriptions;
using Kadr.Platform.Domain.Plans;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Subscriptions;

public sealed class SubscriptionService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ISubscriptionGuard guard,
    IActiveEmployeeCounter employeeCounter) : ISubscriptionService
{
    public async Task<Result<SubscriptionDto>> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await guard.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsFailure)
        {
            return Result.Failure<SubscriptionDto>(snapshot.ErrorCode!, snapshot.ErrorMessage!);
        }

        var s = snapshot.Value!;
        var active = tenantContext.TenantId is null
            ? 0
            : await employeeCounter.CountAsync(tenantContext.TenantId.Value, cancellationToken)
                .ConfigureAwait(false);

        return Result.Success(new SubscriptionDto(
            new SubscriptionPlanDto(
                s.PlanCode,
                s.PlanNameAr,
                s.PlanNameEn,
                NormalizePricingModel(s.PricingModel),
                s.EmployeeCap,
                s.UnitPrice,
                s.MinBilledEmployees,
                s.Currency),
            ToApiStatus(s.PersistedStatus),
            s.EffectiveState,
            s.ReadOnly,
            s.StartsAt,
            s.StartsAt,
            s.TrialEndsAt,
            s.DaysLeft,
            new SubscriptionUsageDto(active, s.EmployeeCap),
            s.Features));
    }

    public async Task<Result<IReadOnlyList<PlanCatalogItemDto>>> ListPlansAsync(
        CancellationToken cancellationToken = default)
    {
        var plans = await db.Plans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.EmployeeCap ?? int.MaxValue)
            .ThenBy(p => p.Code)
            .Select(p => new
            {
                p.Code,
                p.NameAr,
                p.NameEn,
                p.PricingModel,
                p.EmployeeCap,
                p.UnitPrice,
                p.MinBilledEmployees,
                p.Currency,
                p.FeaturesJson,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PlanCatalogItemDto> items = plans
            .Select(p => new PlanCatalogItemDto(
                p.Code,
                p.NameAr,
                p.NameEn,
                PricingModelLabel(p.PricingModel),
                p.EmployeeCap,
                p.UnitPrice,
                p.MinBilledEmployees,
                p.Currency,
                PlanFeaturesParser.Parse(p.FeaturesJson)))
            .ToList();

        return Result.Success(items);
    }

    public async Task<Result<MeSubscriptionSummary>> GetMeSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await guard.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsFailure)
        {
            return Result.Failure<MeSubscriptionSummary>(snapshot.ErrorCode!, snapshot.ErrorMessage!);
        }

        var s = snapshot.Value!;
        return Result.Success(new MeSubscriptionSummary(
            s.EffectiveState,
            s.TrialEndsAt,
            s.DaysLeft,
            s.ReadOnly,
            s.Features));
    }

    private static string NormalizePricingModel(string model) =>
        model switch
        {
            nameof(PricingModel.FlatTier) => "flatTier",
            nameof(PricingModel.PerEmployee) => "perEmployee",
            _ => model,
        };

    private static string PricingModelLabel(PricingModel model) =>
        model switch
        {
            PricingModel.FlatTier => "flatTier",
            PricingModel.PerEmployee => "perEmployee",
            _ => model.ToString(),
        };

    private static string ToApiStatus(string persisted) =>
        persisted switch
        {
            nameof(SubscriptionStatus.Trialing) => "trialing",
            nameof(SubscriptionStatus.Active) => "active",
            nameof(SubscriptionStatus.PastDue) => "pastDue",
            nameof(SubscriptionStatus.Cancelled) => "cancelled",
            nameof(SubscriptionStatus.Suspended) => "suspended",
            _ => persisted,
        };
}
