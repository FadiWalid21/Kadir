using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Identity.Domain.Tokens;

/// <summary>Refresh token for platform operators (no tenant scope).</summary>
public sealed class PlatformRefreshToken : Entity, IPlatformEntity
{
    private PlatformRefreshToken()
    {
    }

    public Guid PlatformAdminId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? DeviceInfo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsActive(DateTimeOffset utcNow) =>
        RevokedAt is null && ExpiresAt > utcNow;

    public static PlatformRefreshToken Create(
        Guid platformAdminId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset expiresAt,
        string? createdByIp,
        string? deviceInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new PlatformRefreshToken
        {
            Id = GuidV7.NewGuid(),
            PlatformAdminId = platformAdminId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            ExpiresAt = expiresAt,
            CreatedByIp = createdByIp,
            DeviceInfo = deviceInfo,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Revoke(DateTimeOffset utcNow, Guid? replacedByTokenId = null)
    {
        RevokedAt = utcNow;
        if (replacedByTokenId is not null)
        {
            ReplacedByTokenId = replacedByTokenId;
        }
    }
}
