using Kadr.Platform.Application.Abstractions;

namespace Kadr.Infrastructure.Persistence.Subscriptions;

public sealed class StubActiveEmployeeCounter : IActiveEmployeeCounter
{
    public Task<int> CountAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
