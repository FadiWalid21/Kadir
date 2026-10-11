using Kadr.Platform.Application.Subscriptions;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface ISubscriptionGuard
{
    Task<Result> EnsureWritableAsync(CancellationToken cancellationToken = default);

    Task<Result> EnsureFeatureAsync(string featureKey, CancellationToken cancellationToken = default);

    Task<Result> EnsureEmployeeCapacityAsync(int requestedActiveCount, CancellationToken cancellationToken = default);

    Task<Result<SubscriptionSnapshot>> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
