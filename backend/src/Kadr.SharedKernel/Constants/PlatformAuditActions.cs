namespace Kadr.SharedKernel.Constants;

/// <summary>Audit action codes written by platform-admin operations on a tenant.</summary>
public static class PlatformAuditActions
{
    public const string Prefix = "platform.";
    public const string TenantEntityType = "Tenant";

    public const string PlanChanged = "platform.tenant.plan_changed";
    public const string TrialExtended = "platform.tenant.trial_extended";
    public const string TenantSuspended = "platform.tenant.suspended";
    public const string TenantResumed = "platform.tenant.resumed";
    public const string SubscriptionCancelled = "platform.tenant.subscription_cancelled";
    public const string IndustryChanged = "platform.tenant.industry_changed";
}
