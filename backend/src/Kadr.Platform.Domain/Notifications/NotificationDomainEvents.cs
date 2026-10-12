using Kadr.Platform.Domain.Workflows;
using Kadr.SharedKernel.Domain;

namespace Kadr.Platform.Domain.Notifications;

/// <summary>An approval task was opened for <see cref="RecipientUserId"/>.</summary>
public sealed record ApprovalRequiredNotificationRequested(
    Guid TenantId,
    Guid RecipientUserId,
    Guid InstanceId,
    Guid SubjectId,
    WorkflowRequestType RequestType,
    string RequesterNameAr,
    string RequesterNameEn,
    string LinkPath) : IDomainEvent;

/// <summary>A request was approved or rejected; the requester is notified.</summary>
public sealed record RequestDecisionNotificationRequested(
    Guid TenantId,
    Guid RecipientUserId,
    Guid InstanceId,
    Guid SubjectId,
    WorkflowRequestType RequestType,
    bool Approved,
    string? RejectReason,
    string LinkPath) : IDomainEvent;
