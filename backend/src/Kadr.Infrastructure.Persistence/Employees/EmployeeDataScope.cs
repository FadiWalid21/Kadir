using Kadr.Identity.Domain.Roles;
using Kadr.Platform.Domain.Employees;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Employees;

internal static class EmployeeDataScope
{
    public static async Task<(DataScope Scope, Guid? LinkedEmployeeId, Guid? DepartmentId, Guid? BranchId)> ResolveAsync(
        KadrDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var scopes = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.DataScope)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var scope = scopes.Count == 0 ? DataScope.Own : scopes.Max();

        var linked = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.EmployeeId })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? departmentId = null;
        Guid? branchId = null;
        if (linked?.EmployeeId is Guid empId)
        {
            var emp = await db.Employees.AsNoTracking()
                .Where(e => e.Id == empId)
                .Select(e => new { e.DepartmentId, e.BranchId })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            departmentId = emp?.DepartmentId;
            branchId = emp?.BranchId;
        }

        return (scope, linked?.EmployeeId, departmentId, branchId);
    }

    public static IQueryable<Employee> Apply(
        IQueryable<Employee> query,
        DataScope scope,
        Guid userId,
        Guid? linkedEmployeeId,
        Guid? departmentId,
        Guid? branchId)
    {
        return scope switch
        {
            DataScope.All => query,
            DataScope.Own => query.Where(e =>
                e.UserId == userId || (linkedEmployeeId != null && e.Id == linkedEmployeeId)),
            DataScope.Team => query.Where(e =>
                e.UserId == userId
                || (linkedEmployeeId != null && (e.Id == linkedEmployeeId || e.ManagerEmployeeId == linkedEmployeeId))),
            DataScope.Department => departmentId == null
                ? query.Where(_ => false)
                : query.Where(e => e.DepartmentId == departmentId),
            DataScope.Branch => branchId == null
                ? query.Where(_ => false)
                : query.Where(e => e.BranchId == branchId),
            _ => query.Where(_ => false),
        };
    }
}
