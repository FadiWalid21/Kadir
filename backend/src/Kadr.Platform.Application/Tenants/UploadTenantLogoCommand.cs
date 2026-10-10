using Kadr.SharedKernel.Application;

namespace Kadr.Platform.Application.Tenants;

public sealed record UploadTenantLogoCommand(
    Stream Content,
    string? ContentType,
    string? FileName) : ICommand<TenantLogoResult>;

public sealed record DeleteTenantLogoCommand : ICommand;

public sealed record TenantLogoResult(Guid LogoFileId, string LogoUrl);
