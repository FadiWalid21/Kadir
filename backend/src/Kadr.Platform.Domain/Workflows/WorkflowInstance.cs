using Kadr.Platform.Domain.Notifications;
using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Workflows;

public sealed class WorkflowInstance : AggregateRoot, ITenantOwned
{
    private WorkflowInstance()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid WorkflowId { get; private set; }
    public WorkflowRequestType RequestType { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid RequesterEmployeeId { get; private set; }
    public int CurrentStepOrder { get; private set; }
    public WorkflowInstanceStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static WorkflowInstance Start(
        Guid tenantId,
        Guid workflowId,
        WorkflowRequestType requestType,
        Guid subjectId,
        Guid requesterEmployeeId,
        int firstStepOrder)
    {
        return new WorkflowInstance
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            WorkflowId = workflowId,
            RequestType = requestType,
            SubjectId = subjectId,
            RequesterEmployeeId = requesterEmployeeId,
            CurrentStepOrder = firstStepOrder,
            Status = WorkflowInstanceStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void AdvanceTo(int stepOrder) => CurrentStepOrder = stepOrder;

    public void CompleteApproved()
    {
        Status = WorkflowInstanceStatus.Approved;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void CompleteRejected()
    {
        Status = WorkflowInstanceStatus.Rejected;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = WorkflowInstanceStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Asks for an in-app/email/push notification to the user who must approve the current step.</summary>
    public void NotifyApprovalRequired(Guid recipientUserId, string requesterNameAr, string requesterNameEn)
    {
        Raise(new ApprovalRequiredNotificationRequested(
            TenantId,
            recipientUserId,
            Id,
            SubjectId,
            RequestType,
            requesterNameAr,
            requesterNameEn,
            NotificationLinkPaths.ApprovalInbox(RequestType)));
    }

    /// <summary>Asks for a notification to the requester about the final decision.</summary>
    public void NotifyDecision(Guid recipientUserId, bool approved, string? rejectReason = null)
    {
        Raise(new RequestDecisionNotificationRequested(
            TenantId,
            recipientUserId,
            Id,
            SubjectId,
            RequestType,
            approved,
            string.IsNullOrWhiteSpace(rejectReason) ? null : rejectReason.Trim(),
            NotificationLinkPaths.MyRequests(RequestType)));
    }
}
