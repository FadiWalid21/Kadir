using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.PlatformAdmin;

public sealed record PlatformLoginResult(
    string AccessToken,
    int ExpiresInSeconds,
    string RefreshOpaqueToken,
    DateTimeOffset RefreshExpiresAt,
    Guid PlatformAdminId,
    string FullName,
    string Email);

public sealed record PlatformRotatedRefresh(
    Guid PlatformAdminId,
    string Language,
    string OpaqueToken,
    DateTimeOffset ExpiresAt);

public interface IPlatformAuthService
{
    Task<Result<PlatformLoginResult>> LoginAsync(
        string email,
        string password,
        string? clientIp,
        string? deviceInfo,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformRotatedRefresh>> RefreshAsync(
        string opaqueToken,
        string? clientIp,
        string? deviceInfo,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(string? opaqueToken, CancellationToken cancellationToken = default);
}
