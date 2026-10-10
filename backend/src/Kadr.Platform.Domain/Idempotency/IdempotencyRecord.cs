using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Idempotency;

public sealed class IdempotencyRecord : Entity, IPlatformEntity
{
    private IdempotencyRecord()
    {
    }

    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public int ResponseStatus { get; private set; }
    public string ResponseBody { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public static IdempotencyRecord Create(
        string key,
        string requestHash,
        int responseStatus,
        string responseBody,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        return new IdempotencyRecord
        {
            Id = GuidV7.NewGuid(),
            Key = key.Trim(),
            RequestHash = requestHash,
            ResponseStatus = responseStatus,
            ResponseBody = responseBody,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
        };
    }
}
