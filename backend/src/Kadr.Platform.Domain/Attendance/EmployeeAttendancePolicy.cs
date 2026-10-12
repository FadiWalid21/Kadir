using Kadr.SharedKernel.Domain;

namespace Kadr.Platform.Domain.Attendance;

/// <summary>Per-employee punch exceptions (1:1 with Employee; keyed by employee id).</summary>
public sealed class EmployeeAttendancePolicy : ITenantOwned
{
    private EmployeeAttendancePolicy()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public bool MayClockInFromAnywhere { get; private set; }
    public bool AnywhereRequiresReason { get; private set; } = true;
    public bool AnywhereRequiresPhoto { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static EmployeeAttendancePolicy Create(
        Guid tenantId,
        Guid employeeId,
        bool mayClockInFromAnywhere,
        bool anywhereRequiresReason,
        bool anywhereRequiresPhoto,
        Guid? updatedBy)
    {
        if (employeeId == Guid.Empty)
        {
            throw new ArgumentException("Employee id is required.", nameof(employeeId));
        }

        return new EmployeeAttendancePolicy
        {
            TenantId = tenantId,
            EmployeeId = employeeId,
            MayClockInFromAnywhere = mayClockInFromAnywhere,
            AnywhereRequiresReason = anywhereRequiresReason,
            AnywhereRequiresPhoto = anywhereRequiresPhoto,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = updatedBy,
        };
    }

    public void Update(
        bool mayClockInFromAnywhere,
        bool anywhereRequiresReason,
        bool anywhereRequiresPhoto,
        Guid? updatedBy)
    {
        MayClockInFromAnywhere = mayClockInFromAnywhere;
        AnywhereRequiresReason = anywhereRequiresReason;
        AnywhereRequiresPhoto = anywhereRequiresPhoto;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}
