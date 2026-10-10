using Kadr.SharedKernel.Application;

namespace Kadr.Platform.Application.Tenants;

public sealed record RegisterTenantCommand(
    string FullName,
    string Email,
    string Password,
    string CompanyName,
    string IndustryCode,
    string? TimeZone,
    string? DefaultLanguage,
    string? CountryCode,
    string? IdempotencyKey = null) : ICommand<RegisterTenantResult>;

public sealed record RegisterTenantResult(
    Guid TenantId,
    string Slug,
    DateTimeOffset TrialEndsAt);
