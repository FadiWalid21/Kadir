using Kadr.Platform.Application.PlatformPlans;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

/// <summary>Platform admin view and edit of the global plan catalog (plans are not tenant-owned).</summary>
public interface IPlatformPlanService
{
    Task<Result<PlatformPlanListResultDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<PlatformPlanDto>> UpdateAsync(
        Guid planId,
        PlatformUpdatePlanRequest request,
        CancellationToken cancellationToken = default);
}
