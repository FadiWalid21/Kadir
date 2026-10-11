using Kadr.Platform.Application.Subscriptions;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface ISubscriptionService
{
    Task<Result<SubscriptionDto>> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PlanCatalogItemDto>>> ListPlansAsync(CancellationToken cancellationToken = default);

    Task<Result<MeSubscriptionSummary>> GetMeSummaryAsync(CancellationToken cancellationToken = default);
}
