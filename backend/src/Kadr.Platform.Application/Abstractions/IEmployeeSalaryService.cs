using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IEmployeeSalaryService
{
    Task<Result<PagedEmployeeSalariesDto>> ListAsync(
        Guid employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeSalaryDto?>> GetCurrentAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeSalaryDto>> GetAsync(
        Guid employeeId,
        Guid salaryId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeSalaryDto>> CreateAsync(
        Guid employeeId,
        CreateEmployeeSalaryRequest request,
        CancellationToken cancellationToken = default);
}
