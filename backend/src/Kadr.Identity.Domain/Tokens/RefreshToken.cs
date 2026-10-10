using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Identity.Domain.Tokens;

public sealed class RefreshToken : Entity, ITenantOwned
{
    private RefreshToken()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
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

    public static RefreshToken Create(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset expiresAt,
        string? createdByIp,
        string? deviceInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
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
