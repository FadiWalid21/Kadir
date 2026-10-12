using Kadr.Platform.Application.Branches;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IBranchSettingsService
{
    Task<Result<PagedBranchesDto>> ListAsync(
        int page,
        int pageSize,
        string? q,
        bool? active,
        CancellationToken cancellationToken = default);

    Task<Result<BranchListItemDto>> CreateAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<BranchListItemDto>> UpdateAsync(
        Guid id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<BranchListItemDto>> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
