using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Attendance;

public sealed class ShiftAssignment : Entity, ITenantOwned
{
    private ShiftAssignment()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public Guid? ShiftId { get; private set; }
    public bool IsDayOff { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ShiftAssignment Create(
        Guid tenantId,
        Guid employeeId,
        DateOnly workDate,
        Guid? shiftId,
        bool isDayOff)
    {
        if (!isDayOff && shiftId is null)
        {
            throw new ArgumentException("Shift is required unless the day is off.");
        }

        return new ShiftAssignment
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            WorkDate = workDate,
            ShiftId = isDayOff ? null : shiftId,
            IsDayOff = isDayOff,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Reassign(Guid? shiftId, bool isDayOff)
    {
        if (!isDayOff && shiftId is null)
        {
            throw new ArgumentException("Shift is required unless the day is off.");
        }

        IsDayOff = isDayOff;
        ShiftId = isDayOff ? null : shiftId;
    }
}

