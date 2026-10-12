using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Leave;

public sealed class LeaveRequest : AggregateRoot, ITenantOwned
{
    private LeaveRequest()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid LeaveTypeId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsHalfDay { get; private set; }
    public decimal Days { get; private set; }
    public string? Reason { get; private set; }
    public Guid? AttachmentFileId { get; private set; }
    public LeaveRequestStatus Status { get; private set; }
    public Guid? WorkflowInstanceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public string? RejectReason { get; private set; }

    public static LeaveRequest Create(
        Guid tenantId,
        Guid employeeId,
        Guid leaveTypeId,
        DateOnly startDate,
        DateOnly endDate,
        bool isHalfDay,
        decimal days,
        string? reason,
        Guid? attachmentFileId,
        Guid? createdBy)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be on or after start date.");
        }

        if (days <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        return new LeaveRequest
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            StartDate = startDate,
            EndDate = endDate,
            IsHalfDay = isHalfDay,
            Days = days,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            AttachmentFileId = attachmentFileId,
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
            throw new InvalidOperationException("Only pending leave can be cancelled.");
        }

        Status = LeaveRequestStatus.Cancelled;
    }
}
