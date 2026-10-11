namespace Kadr.SharedKernel.Constants;

/// <summary>Effective subscription state computed at request time (not persisted).</summary>
public static class SubscriptionStates
{
    public const string TrialRunning = "trialRunning";
    public const string TrialExpired = "trialExpired";
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Cancelled = "cancelled";
    public const string PastDue = "pastDue";
}
