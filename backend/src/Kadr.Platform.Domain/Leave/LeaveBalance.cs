using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Leave;

public sealed class LeaveBalance : Entity, ITenantOwned
{
    private LeaveBalance()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public int Year { get; private set; }
    public decimal Entitled { get; private set; }
    public decimal Accrued { get; private set; }
    public decimal Used { get; private set; }
    public decimal Adjustment { get; private set; }
    public decimal CarriedIn { get; private set; }

    public decimal Remaining => Entitled + Accrued + CarriedIn + Adjustment - Used;

    public static LeaveBalance Create(
        Guid tenantId,
        Guid employeeId,
        Guid leaveTypeId,
        int year,
        decimal entitled)
    {
        return new LeaveBalance
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            Year = year,
            Entitled = entitled,
        };
    }

    public void AddAdjustment(decimal delta) => Adjustment += delta;

    public void AddUsed(decimal days)
    {
        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        Used += days;
    }

    public void SetAccrued(decimal accrued) => Accrued = accrued;

    public void SetCarriedIn(decimal carriedIn) => CarriedIn = carriedIn;
}
