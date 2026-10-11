using Kadr.SharedKernel.Results;

namespace Kadr.Identity.Application.Auth;

public sealed record IssuedRefreshToken(string OpaqueToken, Guid FamilyId, DateTimeOffset ExpiresAt);

public sealed record RotatedRefreshToken(
    string OpaqueToken,
    Guid FamilyId,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    Guid TenantId,
    IReadOnlyList<string> Roles,
    string Language);

public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(
        Guid tenantId,
        Guid userId,
        string? createdByIp,
        string? deviceInfo,
        CancellationToken cancellationToken = default);

    Task<Result<RotatedRefreshToken>> RotateAsync(
        string opaqueToken,
        string? createdByIp,
        string? deviceInfo,
        CancellationToken cancellationToken = default);

    Task RevokeFamilyByOpaqueTokenAsync(
        string opaqueToken,
        CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
