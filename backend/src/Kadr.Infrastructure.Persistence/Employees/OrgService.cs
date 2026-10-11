using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Employees;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Employees;

public sealed class OrgService(
    KadrDbContext db,
    ISubscriptionGuard subscriptionGuard) : IOrgService
{
    public async Task<Result<IReadOnlyList<OrgDepartmentNodeDto>>> GetTreeAsync(
        CancellationToken cancellationToken = default)
    {
        var feature = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.CoreHr, cancellationToken)
            .ConfigureAwait(false);
        if (feature.IsFailure)
        {
            return Result.Failure<IReadOnlyList<OrgDepartmentNodeDto>>(feature.ErrorCode!, feature.ErrorMessage!);
        }

        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.IsActive)
            .Select(d => new
            {
                d.Id,
                d.NameAr,
                d.NameEn,
                d.ParentId,
                d.ManagerEmployeeId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var counts = await db.Employees.AsNoTracking()
            .Where(e => e.DepartmentId != null)
            .GroupBy(e => e.DepartmentId!.Value)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        var managerIds = departments
            .Where(d => d.ManagerEmployeeId is not null)
            .Select(d => d.ManagerEmployeeId!.Value)
            .Distinct()
            .ToList();
        var managers = managerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Employees.AsNoTracking()
                .Where(e => managerIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.FullNameAr, cancellationToken)
                .ConfigureAwait(false);

        var nodes = departments.ToDictionary(
            d => d.Id,
            d => new OrgDepartmentNodeDto(
                d.Id,
                d.NameAr,
                d.NameEn,
                d.ParentId,
                d.ManagerEmployeeId,
                d.ManagerEmployeeId is Guid mid && managers.TryGetValue(mid, out var name) ? name : null,
                counts.GetValueOrDefault(d.Id),
                []));

        var roots = new List<OrgDepartmentNodeDto>();
        var childrenMap = departments
            .GroupBy(d => d.ParentId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.Select(x => x.Id).ToList());

        OrgDepartmentNodeDto Build(Guid id)
        {
            var n = nodes[id];
            var childIds = childrenMap.GetValueOrDefault(id) ?? [];
            var children = childIds.Select(Build).OrderBy(c => c.NameAr).ToList();
            return n with { Children = children };
        }

        foreach (var d in departments.Where(d => d.ParentId is null || !nodes.ContainsKey(d.ParentId.Value)))
        {
            roots.Add(Build(d.Id));
        }

        return Result.Success<IReadOnlyList<OrgDepartmentNodeDto>>(
            roots.OrderBy(r => r.NameAr).ToList());
    }

    public async Task<Result<OrgLookupsDto>> GetLookupsAsync(CancellationToken cancellationToken = default)
    {
        var feature = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.CoreHr, cancellationToken)
            .ConfigureAwait(false);
        if (feature.IsFailure)
        {
            return Result.Failure<OrgLookupsDto>(feature.ErrorCode!, feature.ErrorMessage!);
        }

        var branches = await db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new OrgLookupItemDto(b.Id, b.Name, b.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.NameAr)
            .Select(d => new OrgLookupItemDto(d.Id, d.NameAr, d.NameEn))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var jobTitles = await db.JobTitles.AsNoTracking()
            .Where(j => j.IsActive)
            .OrderBy(j => j.NameAr)
            .Select(j => new OrgLookupItemDto(j.Id, j.NameAr, j.NameEn))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var documentTypes = await db.DocumentTypes.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.NameAr)
            .Select(d => new DocumentTypeLookupDto(d.Id, d.NameAr, d.NameEn, d.HasExpiry))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new OrgLookupsDto(branches, departments, jobTitles, documentTypes));
    }

    public async Task<Result> UpdateDepartmentAsync(
        Guid departmentId,
        UpdateDepartmentHierarchyRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await subscriptionGuard.EnsureWritableAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return writable;
        }

        var feature = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.CoreHr, cancellationToken)
            .ConfigureAwait(false);
        if (feature.IsFailure)
        {
            return feature;
        }

        var department = await db.Departments
            .SingleOrDefaultAsync(d => d.Id == departmentId, cancellationToken)
            .ConfigureAwait(false);
        if (department is null)
        {
            return Result.Failure(ErrorCodes.OrgNotFound, ErrorCodes.OrgNotFound);
        }

        if (request.ParentId is Guid parentId)
        {
            if (parentId == departmentId
                || await WouldCreateDepartmentCycleAsync(departmentId, parentId, cancellationToken)
                    .ConfigureAwait(false))
            {
                return Result.Failure(ErrorCodes.OrgDepartmentCycle, ErrorCodes.OrgDepartmentCycle);
            }

            if (!await db.Departments.AnyAsync(d => d.Id == parentId, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure(ErrorCodes.OrgNotFound, ErrorCodes.OrgNotFound);
            }
        }

        if (request.ManagerEmployeeId is Guid mid
            && !await db.Employees.AnyAsync(e => e.Id == mid, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(ErrorCodes.EmployeesNotFound, ErrorCodes.EmployeesNotFound);
        }

        department.SetHierarchy(request.ParentId, request.ManagerEmployeeId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private async Task<bool> WouldCreateDepartmentCycleAsync(
        Guid departmentId,
        Guid parentId,
        CancellationToken cancellationToken)
    {
        var current = parentId;
        for (var i = 0; i < 50; i++)
        {
            if (current == departmentId)
            {
                return true;
            }

            var next = await db.Departments.AsNoTracking()
                .Where(d => d.Id == current)
                .Select(d => d.ParentId)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (next is null)
            {
                return false;
            }

            current = next.Value;
        }

        return true;
    }
}
