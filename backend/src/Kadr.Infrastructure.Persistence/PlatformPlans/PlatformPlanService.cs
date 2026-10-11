using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.PlatformPlans;
using Kadr.Platform.Application.Subscriptions;
using Kadr.Platform.Domain.Plans;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.PlatformPlans;

/// <summary>
/// Platform admin catalog edits. Plans are global (not tenant-owned), so no tenant filter applies and no
/// IgnoreQueryFilters is needed. Codes and pricing models are fixed; only seeded plans can be edited.
/// </summary>
public sealed class PlatformPlanService(KadrDbContext db, ITenantContext actor) : IPlatformPlanService
{
    public async Task<Result<PlatformPlanListResultDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var plans = await db.Plans.AsNoTracking()
            .OrderBy(p => p.PricingModel)
            .ThenBy(p => p.UnitPrice)
            .ThenBy(p => p.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PlatformPlanListResultDto(plans.Select(ToDto).ToList()));
    }

    public async Task<Result<PlatformPlanDto>> UpdateAsync(
        Guid planId,
        PlatformUpdatePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = await db.Plans.SingleOrDefaultAsync(p => p.Id == planId, cancellationToken).ConfigureAwait(false);
        if (plan is null)
        {
            return Failure(ErrorCodes.PlatformPlanNotFound);
        }

        if (!PlatformPlanFeatures.TryNormalize(request.FeaturesJson, out var featuresJson))
        {
            return Failure(ErrorCodes.PlatformValidationFailed);
        }

        // Shape depends on the pricing model, which the request cannot change.
        var shapeValid = plan.PricingModel switch
        {
            PricingModel.FlatTier => request.EmployeeCap is not null
                                     && request.UnitPrice is not null
                                     && request.MinBilledEmployees is null,
            PricingModel.PerEmployee => request.UnitPrice is not null
                                        && request.MinBilledEmployees is not null,
            _ => false,
        };
        if (!shapeValid)
        {
            return Failure(ErrorCodes.PlatformValidationFailed);
        }

        // New trials are created on this plan; deactivating it would break registration.
        if (!request.IsActive && string.Equals(plan.Code, PlanCodes.TrialDefault, StringComparison.Ordinal))
        {
            return Failure(ErrorCodes.PlatformPlanProtected);
        }

        plan.UpdateCatalog(
            request.NameAr.Trim(),
            request.NameEn.Trim(),
            request.EmployeeCap,
            request.UnitPrice,
            request.MinBilledEmployees,
            featuresJson,
            request.IsActive,
            actor.UserId);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToDto(plan));
    }

    private static Result<PlatformPlanDto> Failure(string code) => Result.Failure<PlatformPlanDto>(code, code);

    private static PlatformPlanDto ToDto(Plan p) => new(
        p.Id,
        p.Code,
        p.NameAr,
        p.NameEn,
        p.PricingModel.ToString(),
        p.EmployeeCap,
        p.UnitPrice,
        p.MinBilledEmployees,
        p.Currency,
        p.FeaturesJson,
        PlanFeaturesParser.Parse(p.FeaturesJson),
        p.IsActive,
        p.CreatedAt,
        p.UpdatedAt);
}
