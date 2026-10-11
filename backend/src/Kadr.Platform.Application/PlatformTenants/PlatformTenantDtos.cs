namespace Kadr.Platform.Application.PlatformTenants;

public sealed record PlatformTenantListQuery(
    string? Q,
    string? Status,
    string? Plan,
    string? Industry,
    int Page,
    int PageSize);

public sealed record PlatformTenantListItemDto(
    Guid Id,
    string Name,
    string Slug,
    string IndustryCode,
    string Status,
    string PlanCode,
    string PlanNameAr,
    string PlanNameEn,
    string SubscriptionStatus,
    DateTimeOffset? TrialEndsAt,
    int UserCount,
    int ActiveEmployeeCount,
    DateTimeOffset CreatedAt);

public sealed record PlatformTenantSummaryDto(int Total, int Trial, int ExpiringSoon, int Suspended);

public sealed record PlatformTenantListResultDto(
    IReadOnlyList<PlatformTenantListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    PlatformTenantSummaryDto Summary);

public sealed record PlatformTenantCompanyDto(
    string Name,
    string Slug,
    string IndustryCode,
    string DefaultLanguage,
    string TimeZone,
    string Status,
    bool HasLogo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TrialEndsAt);

public sealed record PlatformTenantOwnerDto(
    Guid UserId,
    string FullName,
    string Email,
    bool IsActive,
    DateTimeOffset? LastLoginAt);

public sealed record PlatformTenantUsageDto(
    int UserCount,
    int ActiveUserCount,
    int ActiveEmployeeCount,
    int? EmployeeCap);

public sealed record PlatformTenantSubscriptionDto(
    string PlanCode,
    string PlanNameAr,
    string PlanNameEn,
    string PricingModel,
    int? EmployeeCap,
    string Status,
    string EffectiveState,
    bool ReadOnly,
    DateTimeOffset StartsAt,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? RenewsAt,
    DateTimeOffset? CancelledAt,
    int? DaysLeft);

public sealed record PlatformTenantDetailDto(
    Guid Id,
    PlatformTenantCompanyDto Company,
    PlatformTenantOwnerDto? Owner,
    PlatformTenantUsageDto Usage,
    PlatformTenantSubscriptionDto? Subscription);

public sealed record PlatformAuditCategoryCountDto(string Category, int Last7Days, int Last30Days);

public sealed record PlatformTenantEventDto(
    Guid Id,
    DateTimeOffset At,
    string Action,
    string EntityType,
    Guid? UserId,
    string? UserName);

public sealed record PlatformActionDto(
    Guid Id,
    DateTimeOffset At,
    string Action,
    Guid? AdminId,
    string? AdminName,
    string? AdminEmail,
    string? Reason);

public sealed record PlatformTenantAuditSummaryDto(
    IReadOnlyList<PlatformAuditCategoryCountDto> Categories,
    IReadOnlyList<PlatformTenantEventDto> RecentTenantEvents,
    IReadOnlyList<PlatformActionDto> PlatformActions);

public sealed record PlatformChangePlanRequest(string PlanCode, string Reason);

public sealed record PlatformExtendTrialRequest(int Days, string Reason);

public sealed record PlatformReasonRequest(string Reason);

public sealed record PlatformChangeIndustryRequest(string IndustryCode, string Mode, string Reason);

public sealed record PlatformChangeIndustryResultDto(
    string OldIndustryCode,
    string NewIndustryCode,
    string Mode,
    int AddedCount,
    int DeactivatedCount,
    PlatformTenantDetailDto Tenant);

public static class PlatformTenantFilters
{
    public static readonly IReadOnlyList<int> AllowedTrialExtensionDays = [7, 14, 30];
    public const int ExpiringSoonDays = 7;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;
    public const int MaxReasonLength = 1000;
}

public static class TemplateSyncModes
{
    public const string AddMissing = "AddMissing";
    public const string ReplaceUntouched = "ReplaceUntouched";

    public static bool IsValid(string? mode) =>
        string.Equals(mode, AddMissing, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, ReplaceUntouched, StringComparison.OrdinalIgnoreCase);

    public static bool IsReplaceUntouched(string? mode) =>
        string.Equals(mode, ReplaceUntouched, StringComparison.OrdinalIgnoreCase);
}
