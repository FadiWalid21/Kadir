using Kadr.Platform.Domain.Subscriptions;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Subscriptions;

public static class SubscriptionStateCalculator
{
    public static (string State, bool ReadOnly, int? DaysLeft) Compute(
        SubscriptionStatus status,
        DateTimeOffset? trialEndsAt,
        DateTimeOffset? cancelledAt,
        DateTimeOffset utcNow)
    {
        if (status == SubscriptionStatus.Cancelled || cancelledAt is not null)
        {
            return (SubscriptionStates.Cancelled, true, null);
        }

        if (status == SubscriptionStatus.Suspended)
        {
            return (SubscriptionStates.Suspended, true, DaysLeftFrom(trialEndsAt, utcNow));
        }

        if (status == SubscriptionStatus.PastDue)
        {
            return (SubscriptionStates.PastDue, true, null);
        }

        if (status == SubscriptionStatus.Trialing)
        {
            if (trialEndsAt is null || utcNow >= trialEndsAt.Value)
            {
                return (SubscriptionStates.TrialExpired, true, 0);
            }

            var days = DaysLeftFrom(trialEndsAt, utcNow);
            return (SubscriptionStates.TrialRunning, false, days);
        }

        // Active
        return (SubscriptionStates.Active, false, null);
    }

    private static int? DaysLeftFrom(DateTimeOffset? endsAt, DateTimeOffset utcNow)
    {
        if (endsAt is null)
        {
            return null;
        }

        var remaining = endsAt.Value - utcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Ceiling(remaining.TotalDays);
    }
}
