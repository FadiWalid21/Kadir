using Kadr.Platform.Application.Home;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IHomeDashboardService
{
    Task<Result<HomeDashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default);
}
