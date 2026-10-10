using Kadr.SharedKernel.Application;

namespace Kadr.Identity.Application.Auth;

public sealed record LoginCommand(
    string Email,
    string Password,
    string CompanySlug,
    string? ClientIp,
    string? DeviceInfo) : ICommand<LoginResult>;

public sealed record LoginResult(
    string AccessToken,
    int ExpiresInSeconds,
    string RefreshOpaqueToken,
    DateTimeOffset RefreshExpiresAt);
