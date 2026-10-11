namespace Kadr.Platform.Application.Abstractions;

/// <summary>
/// Cross-tenant Google identity lookup. The only sanctioned production use of
/// IgnoreQueryFilters for Google sign-in candidate resolution.
/// </summary>
public interface IGoogleAccountResolver
{
    Task<GoogleAccountResolution> ResolveAsync(
        string providerSubject,
        string normalizedEmail,
        CancellationToken cancellationToken = default);
}

public sealed record GoogleCandidate(
    Guid TenantId,
    Guid UserId,
    string TenantName,
    string RoleName,
    DateTimeOffset? LastLoginAt,
    bool IsActive,
    bool EmailConfirmed,
    bool LinkedBySubject);

public sealed record GoogleAccountResolution(
    IReadOnlyList<GoogleCandidate> ActiveCandidates,
    bool HasUnconfirmedEmailMatch,
    bool HasOnlyInactiveMatches);
