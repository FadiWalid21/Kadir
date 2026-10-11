using Kadr.Platform.Application.Subscriptions;

namespace Kadr.Platform.Application.Abstractions;

public interface ISubscriptionCache
{
    bool TryGet(Guid tenantId, out SubscriptionSnapshot? snapshot);

    void Set(Guid tenantId, SubscriptionSnapshot snapshot);

    void Invalidate(Guid tenantId);
}
