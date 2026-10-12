using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Leave;

public sealed class LeaveBalanceAdjustment : Entity, ITenantOwned
{
    private LeaveBalanceAdjustment()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public int Year { get; private set; }
    public decimal DeltaDays { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static LeaveBalanceAdjustment Create(
        Guid tenantId,
        Guid employeeId,
        Guid leaveTypeId,
        int year,
        decimal deltaDays,
        string reason,
        Guid? createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (deltaDays == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaDays));
        }

        return new LeaveBalanceAdjustment
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            Year = year,
            DeltaDays = deltaDays,
            Reason = reason.Trim(),
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
