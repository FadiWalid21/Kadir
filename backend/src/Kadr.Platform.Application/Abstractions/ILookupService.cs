using Kadr.Platform.Application.Lookups;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface ILookupService
{
    Task<Result<PagedLookupDto>> SearchEmployeesAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default);

    Task<Result<PagedLookupDto>> SearchDepartmentsAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default);

    Task<Result<PagedLookupDto>> SearchBranchesAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default);

    Task<Result<PagedLookupDto>> SearchLegalEntitiesAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default);

    Task<Result<PagedLookupDto>> SearchJobTitlesAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default);
}
