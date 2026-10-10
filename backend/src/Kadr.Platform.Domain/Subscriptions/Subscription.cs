using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Subscriptions;

public sealed class Subscription : AggregateRoot, IPlatformEntity, IAuditable
{
    private Subscription()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
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
            RenewsAt = endsAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
