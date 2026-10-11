using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Identity.Domain.Tokens;

public sealed class PasswordResetToken : Entity, ITenantOwned
{
    private PasswordResetToken()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static PasswordResetToken Create(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new PasswordResetToken
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public bool TryConsume(DateTimeOffset utcNow)
    {
        if (ConsumedAt is not null || ExpiresAt <= utcNow)
        {
            return false;
        }

        ConsumedAt = utcNow;
        return true;
    }

    public void Invalidate(DateTimeOffset utcNow)
    {
        if (ConsumedAt is null)
        {
            ConsumedAt = utcNow;
        }
    }
}
