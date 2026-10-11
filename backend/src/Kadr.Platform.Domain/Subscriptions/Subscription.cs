using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Subscriptions;

public sealed class Subscription : AggregateRoot, ITenantOwned, IAuditable
{
    private Subscription()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? TrialEndsAt { get; private set; }
    public DateTimeOffset? RenewsAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Subscription CreateTrial(Guid tenantId, Guid planId, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        return new Subscription
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Trialing,
            StartsAt = startsAt,
            TrialEndsAt = endsAt,
            RenewsAt = endsAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void ChangePlan(Guid planId)
    {
        if (planId == Guid.Empty)
        {
            throw new ArgumentException("Plan id is required.", nameof(planId));
        }

        PlanId = planId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ExtendTrial(DateTimeOffset newEndsAt)
    {
        Status = SubscriptionStatus.Trialing;
        TrialEndsAt = newEndsAt;
        RenewsAt = newEndsAt;
        CancelledAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ExpireTrial()
    {
        Status = SubscriptionStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSuspended()
    {
        Status = SubscriptionStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate(DateTimeOffset? renewsAt)
    {
        Status = SubscriptionStatus.Active;
        RenewsAt = renewsAt;
        CancelledAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        Status = SubscriptionStatus.Cancelled;
        CancelledAt = cancelledAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
