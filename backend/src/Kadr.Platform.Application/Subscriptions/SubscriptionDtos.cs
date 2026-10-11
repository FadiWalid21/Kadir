namespace Kadr.Platform.Application.Subscriptions;

public sealed record SubscriptionDto(
    SubscriptionPlanDto Plan,
    string Status,
    string EffectiveState,
    bool ReadOnly,
    DateTimeOffset StartsAt,
    DateTimeOffset? TrialStartsAt,
    DateTimeOffset? TrialEndsAt,
    int? DaysLeft,
    SubscriptionUsageDto Usage,
    IReadOnlyDictionary<string, bool> Features);

public sealed record SubscriptionPlanDto(
    string Code,
    string NameAr,
    string NameEn,
    string PricingModel,
    int? EmployeeCap,
    decimal? UnitPrice,
    int? MinBilledEmployees,
    string Currency);

public sealed record SubscriptionUsageDto(int ActiveEmployees, int? Cap);

public sealed record PlanCatalogItemDto(
    string Code,
    string NameAr,
    string NameEn,
    string PricingModel,
    int? EmployeeCap,
    decimal? UnitPrice,
    int? MinBilledEmployees,
    string Currency,
    IReadOnlyDictionary<string, bool> Features);

public sealed record MeSubscriptionSummary(
    string State,
    DateTimeOffset? TrialEndsAt,
    int? DaysLeft,
    bool ReadOnly,
    IReadOnlyDictionary<string, bool> Features);

public sealed record SubscriptionSnapshot(
    Guid SubscriptionId,
    Guid TenantId,
    Guid PlanId,
    string PlanCode,
    string PlanNameAr,
    string PlanNameEn,
    string PricingModel,
    int? EmployeeCap,
    decimal? UnitPrice,
    int? MinBilledEmployees,
    string Currency,
    string PersistedStatus,
    string EffectiveState,
    bool ReadOnly,
    DateTimeOffset StartsAt,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? RenewsAt,
    int? DaysLeft,
    IReadOnlyDictionary<string, bool> Features);
