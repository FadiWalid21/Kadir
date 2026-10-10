namespace Kadr.Platform.Domain.Subscriptions;

public enum SubscriptionStatus
{
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    Cancelled = 3,
}
