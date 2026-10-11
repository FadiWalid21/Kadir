using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Subscriptions;
using Microsoft.Extensions.Caching.Memory;

namespace Kadr.Infrastructure.Persistence.Subscriptions;

public sealed class MemorySubscriptionCache(IMemoryCache cache) : ISubscriptionCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(45);

    public bool TryGet(Guid tenantId, out SubscriptionSnapshot? snapshot)
    {
        if (cache.TryGetValue(Key(tenantId), out SubscriptionSnapshot? cached) && cached is not null)
        {
            snapshot = cached;
            return true;
        }

        snapshot = null;
        return false;
    }

    public void Set(Guid tenantId, SubscriptionSnapshot snapshot) =>
        cache.Set(Key(tenantId), snapshot, Ttl);

    public void Invalidate(Guid tenantId) => cache.Remove(Key(tenantId));

    private static string Key(Guid tenantId) => $"subscription:{tenantId:D}";
}
