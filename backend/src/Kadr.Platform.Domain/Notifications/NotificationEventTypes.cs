namespace Kadr.Platform.Domain.Notifications;

/// <summary>Stable event catalog codes. Preferences and notifications reference these values.</summary>
public static class NotificationEventTypes
{
    public const string WorkflowApprovalRequired = "workflow.approval_required";
    public const string WorkflowRequestApproved = "workflow.request_approved";
    public const string WorkflowRequestRejected = "workflow.request_rejected";
    public const string InvitationAccepted = "invitation.accepted";

    public static readonly IReadOnlyList<string> All =
    [
        WorkflowApprovalRequired,
        WorkflowRequestApproved,
        WorkflowRequestRejected,
        InvitationAccepted,
    ];

    public static bool IsKnown(string? code) =>
        code is not null && All.Contains(code, StringComparer.Ordinal);
}
