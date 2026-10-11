using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Identity.Domain.Tokens;

public enum AuthOneTimeTokenPurpose
{
    GoogleNonce = 1,
    GoogleSelection = 2,
    GoogleRegistration = 3,
}

/// <summary>Platform-scoped one-time tokens (nonce, Google selection/registration). Not tenant-owned.</summary>
public sealed class AuthOneTimeToken : Entity, IPlatformEntity
{
    private AuthOneTimeToken()
    {
    }

    public AuthOneTimeTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string? PayloadJson { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static AuthOneTimeToken Create(
        AuthOneTimeTokenPurpose purpose,
        string tokenHash,
        string? payloadJson,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new AuthOneTimeToken
        {
            Id = GuidV7.NewGuid(),
            Purpose = purpose,
            TokenHash = tokenHash,
            PayloadJson = payloadJson,
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
}
