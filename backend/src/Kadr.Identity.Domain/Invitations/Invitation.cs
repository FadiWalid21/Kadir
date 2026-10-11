using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Identity.Domain.Invitations;

public sealed class Invitation : Entity, ITenantOwned
{
    private Invitation()
    {
    }

    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public Guid RoleId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }

    public static Invitation Create(
        Guid tenantId,
        string email,
        string normalizedEmail,
        Guid roleId,
        string tokenHash,
        DateTimeOffset expiresAt,
        Guid? createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new Invitation
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            RoleId = roleId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public bool IsPending(DateTimeOffset utcNow) =>
        AcceptedAt is null && RevokedAt is null && ExpiresAt > utcNow;

    public void Resend(string tokenHash, DateTimeOffset expiresAt, Guid roleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        RoleId = roleId;
        RevokedAt = null;
    }

    public void Revoke(DateTimeOffset utcNow) => RevokedAt = utcNow;

    public void MarkAccepted(DateTimeOffset utcNow) => AcceptedAt = utcNow;
}
