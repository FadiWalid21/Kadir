using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Domain.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Api.Http;

public sealed class IdempotencyService(KadrDbContext db)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task<IdempotencyLookup?> FindAsync(string key, CancellationToken cancellationToken)
    {
        var record = await db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == key, cancellationToken)
            .ConfigureAwait(false);

        if (record is null || record.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return null;
        }

        return new IdempotencyLookup(record.RequestHash, record.ResponseStatus, record.ResponseBody);
    }

    public async Task StoreAsync(
        string key,
        string requestHash,
        int status,
        object responseBody,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(responseBody);
        db.IdempotencyRecords.Add(
            IdempotencyRecord.Create(key, requestHash, status, json, DateTimeOffset.UtcNow.Add(Ttl)));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static string HashRequest(string body) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
}

public sealed record IdempotencyLookup(string RequestHash, int ResponseStatus, string ResponseBody);
