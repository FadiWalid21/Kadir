using Kadr.Platform.Domain.Notifications;
using Kadr.Platform.Domain.Workflows;

namespace Kadr.Platform.Application.Notifications;

public sealed record NotificationContent(
    string Type,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn);

/// <summary>Bilingual titles and bodies for each event type. Arabic terms follow the design-system glossary.</summary>
public static class NotificationContentBuilder
{
    public static NotificationContent ApprovalRequired(
        WorkflowRequestType requestType,
        string requesterNameAr,
        string requesterNameEn)
    {
        var (ar, en) = RequestLabel(requestType);
        return new NotificationContent(
            NotificationEventTypes.WorkflowApprovalRequired,
            "طلب بانتظار موافقتك",
            "A request needs your approval",
            $"{ar} من {requesterNameAr} بانتظار موافقتك.",
            $"A {en} from {requesterNameEn} is waiting for your approval.");
    }

    public static NotificationContent Decision(
        WorkflowRequestType requestType,
        bool approved,
        string? rejectReason)
    {
        var (ar, en) = RequestLabel(requestType);
        if (approved)
        {
            return new NotificationContent(
                NotificationEventTypes.WorkflowRequestApproved,
                "تمت الموافقة على طلبك",
                "Your request was approved",
                $"تمت الموافقة على {ar} الخاص بك.",
                $"Your {en} was approved.");
        }

        var hasReason = !string.IsNullOrWhiteSpace(rejectReason);
        return new NotificationContent(
            NotificationEventTypes.WorkflowRequestRejected,
            "تم رفض طلبك",
            "Your request was rejected",
            hasReason ? $"تم رفض {ar} الخاص بك. السبب: {rejectReason}" : $"تم رفض {ar} الخاص بك.",
            hasReason ? $"Your {en} was rejected. Reason: {rejectReason}" : $"Your {en} was rejected.");
    }

    public static NotificationContent InvitationAccepted(string inviteeEmail, string inviteeFullName)
    {
        var name = string.IsNullOrWhiteSpace(inviteeFullName) ? inviteeEmail : inviteeFullName.Trim();
        return new NotificationContent(
            NotificationEventTypes.InvitationAccepted,
            "تم قبول الدعوة",
            "Invitation accepted",
            $"{name} قبل دعوتك وانضم إلى الشركة.",
            $"{name} accepted your invitation and joined the company.");
    }

    private static (string Ar, string En) RequestLabel(WorkflowRequestType requestType) => requestType switch
    {
        WorkflowRequestType.Leave => ("طلب الإجازة", "leave request"),
        WorkflowRequestType.Permission => ("طلب الإذن", "permission request"),
        _ => ("طلب تصحيح الحضور", "attendance correction request"),
    };
}
