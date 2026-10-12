using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IEmployeeInsuranceService
{
    Task<Result<EmployeeInsuranceProfileDto?>> GetAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeInsuranceProfileDto>> UpsertAsync(
        Guid employeeId,
        UpsertEmployeeInsuranceRequest request,
        CancellationToken cancellationToken = default);
}
