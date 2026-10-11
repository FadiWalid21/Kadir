using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IDevSubscriptionService
{
    Task<Result> ApplyAsync(DevSubscriptionRequest request, CancellationToken cancellationToken = default);
}

public sealed record DevSubscriptionRequest(
    string? PlanCode,
    int? ExtendDays,
    bool? ExpireTrial,
    DateTimeOffset? TrialEndsAt);
