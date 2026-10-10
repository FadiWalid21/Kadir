using Kadr.SharedKernel.Application;

namespace Kadr.Platform.Application.Tenants;

public sealed record UpdateTenantSettingsCommand(
    string Name,
    string? DefaultLanguage,
    string? TimeZone) : ICommand<TenantSettingsResult>;

public sealed record TenantSettingsResult(
    string Name,
    string DefaultLanguage,
    string TimeZone,
    Guid? LogoFileId,
    string? LogoUrl);
