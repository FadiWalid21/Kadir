using Kadr.Platform.Application.Subscriptions;
using Kadr.Platform.Domain.Subscriptions;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Tests.Subscriptions;

public sealed class SubscriptionStateCalculatorTests
{
    [Fact]
    public void Trialing_before_end_is_trial_running_writable()
    {
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var (state, readOnly, daysLeft) = SubscriptionStateCalculator.Compute(
            SubscriptionStatus.Trialing,
            now.AddDays(5),
            null,
            now);

        Assert.Equal(SubscriptionStates.TrialRunning, state);
        Assert.False(readOnly);
        Assert.Equal(5, daysLeft);
    }

    [Fact]
    public void Trialing_after_end_is_trial_expired_without_job()
    {
        var now = DateTimeOffset.Parse("2026-01-15T00:00:00Z");
        var (state, readOnly, daysLeft) = SubscriptionStateCalculator.Compute(
            SubscriptionStatus.Trialing,
            now.AddDays(-1),
            null,
            now);

        Assert.Equal(SubscriptionStates.TrialExpired, state);
        Assert.True(readOnly);
        Assert.Equal(0, daysLeft);
    }

    [Fact]
    public void Suspended_and_cancelled_are_read_only()
    {
        var now = DateTimeOffset.UtcNow;
        var suspended = SubscriptionStateCalculator.Compute(SubscriptionStatus.Suspended, now, null, now);
        Assert.Equal(SubscriptionStates.Suspended, suspended.State);
        Assert.True(suspended.ReadOnly);

        var cancelled = SubscriptionStateCalculator.Compute(
            SubscriptionStatus.Cancelled,
            null,
            now,
            now);
        Assert.Equal(SubscriptionStates.Cancelled, cancelled.State);
        Assert.True(cancelled.ReadOnly);
    }

    [Fact]
    public void Active_is_writable()
    {
        var (state, readOnly, _) = SubscriptionStateCalculator.Compute(
            SubscriptionStatus.Active,
            null,
            null,
            DateTimeOffset.UtcNow);

        Assert.Equal(SubscriptionStates.Active, state);
        Assert.False(readOnly);
    }
}
