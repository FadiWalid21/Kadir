using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IPayComponentService
{
    Task<Result<PagedPayComponentsDto>> ListAsync(
        int page,
        int pageSize,
        string? q,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<Result<PayComponentDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PayComponentDto>> CreateAsync(
        CreatePayComponentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayComponentDto>> UpdateAsync(
        Guid id,
        UpdatePayComponentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayComponentDto>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
