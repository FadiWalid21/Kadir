using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.PlatformTenants;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.Platform.Domain.Tenants;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.PlatformTenants;

/// <summary>
/// Audited platform-admin mutations. Each operation runs inside a <see cref="PlatformTenantScope"/> so the normal
/// tenant filters and stamping apply to the target tenant, and writes one explicit audit row whose actor is the
/// platform admin and whose tenant is the target.
/// </summary>
public sealed class PlatformTenantCommands(
    KadrDbContext db,
    ITenantContextAccessor tenantContext,
    ITemplateApplyService templateApply,
    ISubscriptionCache cache,
    IAuditLogWriter audit,
    IClock clock) : IPlatformTenantCommands
{
    public async Task<Result> ChangePlanAsync(
        Guid tenantId,
        PlatformChangePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = PlatformTenantScope.Enter(tenantContext, tenantId);

        var loaded = await LoadAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.ErrorCode!, loaded.ErrorMessage!);
        }

        var (_, subscription) = loaded.Value!;
        var planCode = request.PlanCode.Trim();
        var plan = await db.Plans.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Code == planCode && p.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure(ErrorCodes.PlanNotFound, ErrorCodes.PlanNotFound);
        }

        if (plan.Id == subscription.PlanId)
        {
            return Result.Failure(ErrorCodes.PlatformNoChange, ErrorCodes.PlatformNoChange);
        }

        var oldPlanCode = await db.Plans.AsNoTracking()
            .Where(p => p.Id == subscription.PlanId)
            .Select(p => p.Code)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        subscription.ChangePlan(plan.Id);
        StageAudit(
            tenantId,
            PlatformAuditActions.PlanChanged,
            new { planCode = oldPlanCode },
            new { planCode = plan.Code, reason = request.Reason.Trim() });

        await SaveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ExtendTrialAsync(
        Guid tenantId,
        PlatformExtendTrialRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = PlatformTenantScope.Enter(tenantContext, tenantId);

        var loaded = await LoadAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.ErrorCode!, loaded.ErrorMessage!);
        }

        var (tenant, subscription) = loaded.Value!;

        // Only trials (running or expired) can be extended; paid or cancelled subscriptions are not trials.
        if (subscription.Status is not (SubscriptionStatus.Trialing or SubscriptionStatus.Suspended))
        {
            return Result.Failure(ErrorCodes.PlatformTenantStateInvalid, ErrorCodes.PlatformTenantStateInvalid);
        }

        var now = clock.UtcNow;
        var baseEnd = subscription.TrialEndsAt ?? now;
        if (baseEnd < now)
        {
            baseEnd = now;
        }

        var newEnd = baseEnd.AddDays(request.Days);
        var oldEnd = subscription.TrialEndsAt;
        var oldTenantStatus = tenant.Status.ToString();

        subscription.ExtendTrial(newEnd);
        tenant.SetTrialEndsAt(newEnd);
        tenant.ResumeTrial();

        StageAudit(
            tenantId,
            PlatformAuditActions.TrialExtended,
            new { trialEndsAt = oldEnd, tenantStatus = oldTenantStatus },
            new
            {
                trialEndsAt = newEnd,
                tenantStatus = tenant.Status.ToString(),
                days = request.Days,
                reason = request.Reason.Trim(),
            });

        await SaveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> SuspendAsync(
        Guid tenantId,
        PlatformReasonRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = PlatformTenantScope.Enter(tenantContext, tenantId);

        var loaded = await LoadAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.ErrorCode!, loaded.ErrorMessage!);
        }

        var (tenant, subscription) = loaded.Value!;
        if (tenant.Status is TenantStatus.Suspended or TenantStatus.Cancelled)
        {
            return Result.Failure(ErrorCodes.PlatformTenantStateInvalid, ErrorCodes.PlatformTenantStateInvalid);
        }

        var oldTenantStatus = tenant.Status.ToString();
        var oldSubscriptionStatus = subscription.Status.ToString();

        tenant.Suspend();
        subscription.MarkSuspended();

        StageAudit(
            tenantId,
            PlatformAuditActions.TenantSuspended,
            new { tenantStatus = oldTenantStatus, subscriptionStatus = oldSubscriptionStatus },
            new
            {
                tenantStatus = tenant.Status.ToString(),
                subscriptionStatus = subscription.Status.ToString(),
                reason = request.Reason.Trim(),
            });

        await SaveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ResumeAsync(
        Guid tenantId,
        PlatformReasonRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = PlatformTenantScope.Enter(tenantContext, tenantId);

        var loaded = await LoadAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.ErrorCode!, loaded.ErrorMessage!);
        }

        var (tenant, subscription) = loaded.Value!;
        if (tenant.Status != TenantStatus.Suspended)
        {
            return Result.Failure(ErrorCodes.PlatformTenantStateInvalid, ErrorCodes.PlatformTenantStateInvalid);
        }

        var oldTenantStatus = tenant.Status.ToString();
        var oldSubscriptionStatus = subscription.Status.ToString();
        var now = clock.UtcNow;

        if (subscription.TrialEndsAt is { } trialEnd && trialEnd > now)
        {
            // Trial still has time left: go back to trialing with the original end date.
            subscription.ExtendTrial(trialEnd);
            tenant.ResumeTrial();
        }
        else
        {
            // Trial is over: the operator is explicitly re-enabling the account.
            subscription.Activate(subscription.RenewsAt is { } renews && renews > now ? renews : null);
            tenant.Activate();
        }

        StageAudit(
            tenantId,
            PlatformAuditActions.TenantResumed,
            new { tenantStatus = oldTenantStatus, subscriptionStatus = oldSubscriptionStatus },
            new
            {
                tenantStatus = tenant.Status.ToString(),
                subscriptionStatus = subscription.Status.ToString(),
                reason = request.Reason.Trim(),
            });

        await SaveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> CancelSubscriptionAsync(
        Guid tenantId,
        PlatformReasonRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = PlatformTenantScope.Enter(tenantContext, tenantId);

        var loaded = await LoadAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.ErrorCode!, loaded.ErrorMessage!);
        }

        var (_, subscription) = loaded.Value!;
        if (subscription.Status == SubscriptionStatus.Cancelled)
        {
            return Result.Failure(ErrorCodes.PlatformTenantStateInvalid, ErrorCodes.PlatformTenantStateInvalid);
        }

        var oldStatus = subscription.Status.ToString();
        subscription.Cancel(clock.UtcNow);

        StageAudit(
            tenantId,
            PlatformAuditActions.SubscriptionCancelled,
            new { subscriptionStatus = oldStatus },
            new { subscriptionStatus = subscription.Status.ToString(), reason = request.Reason.Trim() });

        await SaveAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<PlatformIndustryChangeOutcome>> ChangeIndustryAsync(
        Guid tenantId,
        PlatformChangeIndustryRequest request,
        CancellationToken cancellationToken = default)
    {
        var newIndustry = request.IndustryCode.Trim().ToLowerInvariant();
        var replace = TemplateSyncModes.IsReplaceUntouched(request.Mode);
        var mode = replace ? TemplateSyncModes.ReplaceUntouched : TemplateSyncModes.AddMissing;

        using var scope = PlatformTenantScope.Enter(tenantContext, tenantId);

        var tenant = await db.Tenants
            .SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure<PlatformIndustryChangeOutcome>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (!IndustryCodes.IsAllowed(newIndustry)
            || !await db.IndustryTemplates.AsNoTracking()
                .AnyAsync(t => t.Code == newIndustry, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<PlatformIndustryChangeOutcome>(ErrorCodes.IndustryInvalid, ErrorCodes.IndustryInvalid);
        }

        if (string.Equals(tenant.IndustryCode, newIndustry, StringComparison.Ordinal))
        {
            return Result.Failure<PlatformIndustryChangeOutcome>(ErrorCodes.PlatformNoChange, ErrorCodes.PlatformNoChange);
        }

        var oldIndustry = tenant.IndustryCode;

        // One transaction: industry code, template re-apply and the audit row commit together.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        tenant.ChangeIndustry(newIndustry);

        int added;
        var deactivated = 0;
        if (replace)
        {
            var result = await templateApply.ReplaceUntouchedAsync(tenantId, newIndustry, cancellationToken)
                .ConfigureAwait(false);
            added = result.AddedCount;
            deactivated = result.DeactivatedCount;
        }
        else
        {
            added = await templateApply.AddMissingAsync(tenantId, newIndustry, cancellationToken)
                .ConfigureAwait(false);
        }

        StageAudit(
            tenantId,
            PlatformAuditActions.IndustryChanged,
            new { industryCode = oldIndustry },
            new
            {
                industryCode = newIndustry,
                mode,
                added,
                deactivated,
                reason = request.Reason.Trim(),
            });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new PlatformIndustryChangeOutcome(oldIndustry, newIndustry, mode, added, deactivated));
    }

    private async Task<Result<(Tenant Tenant, Subscription Subscription)>> LoadAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants
            .SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            return Result.Failure<(Tenant, Subscription)>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        // The scope sets the target tenant, so the standard tenant filter returns exactly this tenant's row.
        var subscription = await db.Subscriptions
            .SingleOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (subscription is null)
        {
            return Result.Failure<(Tenant, Subscription)>(
                ErrorCodes.SubscriptionNotFound,
                ErrorCodes.SubscriptionNotFound);
        }

        return Result.Success((tenant, subscription));
    }

    private void StageAudit(Guid tenantId, string action, object oldValues, object newValues) =>
        audit.Stage(
            tenantId,
            tenantContext.UserId,
            action,
            PlatformAuditActions.TenantEntityType,
            tenantId,
            JsonSerializer.Serialize(oldValues),
            JsonSerializer.Serialize(newValues));

    private async Task SaveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        cache.Invalidate(tenantId);
    }
}
