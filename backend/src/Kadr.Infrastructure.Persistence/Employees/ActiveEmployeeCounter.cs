using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Employees;

public sealed class ActiveEmployeeCounter(KadrDbContext db) : IActiveEmployeeCounter
{
    public Task<int> CountAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        db.Employees.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(
                e => e.TenantId == tenantId
                     && (e.Status == EmployeeStatus.Active
                         || e.Status == EmployeeStatus.Probation
                         || e.Status == EmployeeStatus.OnLeave),
                cancellationToken);
}
