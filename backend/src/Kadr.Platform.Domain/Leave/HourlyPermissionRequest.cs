using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Leave;

public sealed class HourlyPermissionRequest : AggregateRoot, ITenantOwned
{
    private HourlyPermissionRequest()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public decimal Hours { get; private set; }
    public string? Reason { get; private set; }
    public LeaveRequestStatus Status { get; private set; }
    public Guid? WorkflowInstanceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public string? RejectReason { get; private set; }

    public static HourlyPermissionRequest Create(
        Guid tenantId,
        Guid employeeId,
        DateOnly workDate,
        TimeOnly startTime,
        TimeOnly endTime,
        decimal hours,
        string? reason,
        Guid? createdBy)
    {
        if (endTime <= startTime)
        {
            throw new ArgumentException("End time must be after start time.");
        }

        if (hours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hours));
        }

        return new HourlyPermissionRequest
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            WorkDate = workDate,
            StartTime = startTime,
            EndTime = endTime,
            Hours = hours,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Status = LeaveRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public void AttachWorkflow(Guid workflowInstanceId) => WorkflowInstanceId = workflowInstanceId;

    public void Approve(Guid decidedBy)
    {
        Status = LeaveRequestStatus.Approved;
        DecidedAt = DateTimeOffset.UtcNow;
        DecidedBy = decidedBy;
        RejectReason = null;
    }

    public void Reject(Guid decidedBy, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = LeaveRequestStatus.Rejected;
        DecidedAt = DateTimeOffset.UtcNow;
        DecidedBy = decidedBy;
        RejectReason = reason.Trim();
    }

    public void Cancel()
    {
        if (Status != LeaveRequestStatus.Pending)
        {
            throw new InvalidOperationException("Only pending permissions can be cancelled.");
        }

        Status = LeaveRequestStatus.Cancelled;
    }
}
