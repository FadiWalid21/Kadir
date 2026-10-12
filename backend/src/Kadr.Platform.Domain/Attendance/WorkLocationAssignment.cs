using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Attendance;

public sealed class WorkLocationAssignment : Entity, ITenantOwned
{
    private WorkLocationAssignment()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid WorkLocationId { get; private set; }
    public WorkLocationAssignmentScope Scope { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? BranchId { get; private set; }
    public DateOnly? EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static WorkLocationAssignment Create(
        Guid tenantId,
        Guid workLocationId,
        WorkLocationAssignmentScope scope,
        Guid? employeeId,
        Guid? departmentId,
        Guid? branchId,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo)
    {
        if (workLocationId == Guid.Empty)
        {
            throw new ArgumentException("Work location id is required.", nameof(workLocationId));
        }

        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        // Exactly the key that matches the scope must be set.
        var keyOk = scope switch
        {
            WorkLocationAssignmentScope.Employee => employeeId is { } e && e != Guid.Empty
                                                    && departmentId is null && branchId is null,
            WorkLocationAssignmentScope.Department => departmentId is { } d && d != Guid.Empty
                                                      && employeeId is null && branchId is null,
            _ => branchId is { } b && b != Guid.Empty && employeeId is null && departmentId is null,
        };
        if (!keyOk)
        {
            throw new ArgumentException("Assignment target does not match its scope.");
        }

        if (effectiveFrom is not null && effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ArgumentException("Effective-to date must be on or after effective-from date.");
        }

        return new WorkLocationAssignment
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            WorkLocationId = workLocationId,
            Scope = scope,
            EmployeeId = employeeId,
            DepartmentId = departmentId,
            BranchId = branchId,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public bool IsEffectiveOn(DateOnly workDate) =>
        (EffectiveFrom is null || EffectiveFrom <= workDate)
        && (EffectiveTo is null || EffectiveTo >= workDate);
}
