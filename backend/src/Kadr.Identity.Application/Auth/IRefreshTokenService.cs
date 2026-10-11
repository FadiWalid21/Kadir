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

    /// <summary>Revokes every active refresh token of the user except those in the given family.</summary>
    Task RevokeAllExceptFamilyAsync(
        Guid tenantId,
        Guid userId,
        Guid familyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the family of an active refresh token that belongs to the given user.
    /// Returns null when the token is unknown, revoked, expired, or owned by someone else.
    /// </summary>
    Task<Guid?> ResolveActiveFamilyIdAsync(
        Guid tenantId,
        Guid userId,
        string opaqueToken,
        CancellationToken cancellationToken = default);
}
