using Kadr.SharedKernel.Application;

namespace Kadr.Platform.Application.Tenants;

public sealed record RegisterTenantCommand(
    string FullName,
    string Email,
    string? Password,
    string CompanyName,
    string IndustryCode,
    string? TimeZone,
    string? DefaultLanguage,
    string? CountryCode,
    IReadOnlyList<string>? WorkWeekDays = null,
    IReadOnlyList<string>? WorkWeekWeekend = null,
    string? DefaultWorkStart = null,
    string? DefaultWorkEnd = null,
    string? FirstBranchName = null,
    string? IdempotencyKey = null,
    string? GoogleRegistrationToken = null) : ICommand<RegisterTenantResult>;

public sealed record RegisterTenantResult(
    Guid TenantId,
    string Slug,
    DateTimeOffset TrialEndsAt);
