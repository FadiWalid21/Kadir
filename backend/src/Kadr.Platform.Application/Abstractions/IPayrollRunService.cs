using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IPayrollRunService
{
    Task<Result<PayrollRunDetailDto>> CreateAsync(
        CreatePayrollRunRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedPayrollRunsDto>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollRunDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PayrollRunDetailDto>> CalculateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PayrollRunDetailDto>> SubmitAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PayrollRunDetailDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PayrollRunDetailDto>> LockAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PayrollRunDetailDto>> ReopenAsync(
        Guid id,
        ReopenPayrollRunRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedPayrollEntriesDto>> ListEntriesAsync(
        Guid runId,
        int page,
        int pageSize,
        string? q,
        Guid? departmentId,
        bool? warnings,
        bool? held,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryDetailDto>> GetEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollEntryDetailDto>> HoldEntryAsync(
        Guid entryId,
        HoldPayrollEntryRequest request,
        CancellationToken cancellationToken = default);
}
