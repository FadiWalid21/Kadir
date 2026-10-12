using Kadr.Platform.Application.Lookups;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IPlatformLookupService
{
    Task<Result<PagedLookupDto>> SearchTenantsAsync(
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
