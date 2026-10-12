using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IOvertimeMultipliersService
{
    Task<Result<OvertimeMultipliersDto>> GetAsync(CancellationToken cancellationToken = default);
}
