using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Attendance;

public enum AttendanceCorrectionStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
}

public sealed class AttendanceCorrectionRequest : AggregateRoot, ITenantOwned
{
    private AttendanceCorrectionRequest()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public string ProblemType { get; private set; } = string.Empty;
    public TimeOnly ProposedTime { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid? AttachmentFileId { get; private set; }
    public AttendanceCorrectionStatus Status { get; private set; }
    public string? RejectReason { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    public static AttendanceCorrectionRequest Create(
        Guid tenantId,
        Guid employeeId,
        Guid createdByUserId,
        DateOnly workDate,
        string problemType,
        TimeOnly proposedTime,
        string reason,
        Guid? attachmentFileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(problemType);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new AttendanceCorrectionRequest
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            WorkDate = workDate,
            ProblemType = problemType.Trim(),
            ProposedTime = proposedTime,
            Reason = reason.Trim(),
            AttachmentFileId = attachmentFileId,
            Status = AttendanceCorrectionStatus.Pending,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Approve(Guid decidedByUserId)
    {
        if (Status != AttendanceCorrectionStatus.Pending)
        {
            throw new InvalidOperationException("Only pending corrections can be approved.");
        }

        Status = AttendanceCorrectionStatus.Approved;
        DecidedByUserId = decidedByUserId;
        DecidedAt = DateTimeOffset.UtcNow;
        RejectReason = null;
    }

    public void Reject(Guid decidedByUserId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != AttendanceCorrectionStatus.Pending)
        {
            throw new InvalidOperationException("Only pending corrections can be rejected.");
        }

        Status = AttendanceCorrectionStatus.Rejected;
        DecidedByUserId = decidedByUserId;
        DecidedAt = DateTimeOffset.UtcNow;
        RejectReason = reason.Trim();
    }

    public void Cancel()
    {
        if (Status != AttendanceCorrectionStatus.Pending)
        {
            throw new InvalidOperationException("Only pending corrections can be cancelled.");
        }

        Status = AttendanceCorrectionStatus.Cancelled;
        DecidedAt = DateTimeOffset.UtcNow;
    }
}
