using Kadr.Platform.Domain.Workflows;

namespace Kadr.Platform.Domain.Notifications;

/// <summary>App-relative deep links for notification events.</summary>
public static class NotificationLinkPaths
{
    private static readonly HashSet<string> AllowedExact = new(StringComparer.Ordinal)
    {
        "/leave/approvals",
        "/leave/me",
        "/attendance/corrections",
        "/notifications",
        "/",
    };

    public static string ApprovalInbox(WorkflowRequestType requestType) => requestType switch
    {
        WorkflowRequestType.AttendanceCorrection => "/attendance/corrections",
        _ => "/leave/approvals",
    };

    public static string MyRequests(WorkflowRequestType requestType) => requestType switch
    {
        WorkflowRequestType.AttendanceCorrection => "/attendance/corrections",
        _ => "/leave/me",
    };

    /// <summary>True when the path is a known relative app route (no scheme, host, or traversal).</summary>
    public static bool IsAllowed(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();
        if (!trimmed.StartsWith('/')
            || trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.Contains("://", StringComparison.Ordinal)
            || trimmed.Contains('\\', StringComparison.Ordinal)
            || trimmed.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var withoutQuery = trimmed.Split('?', 2)[0];
        return AllowedExact.Contains(withoutQuery);
    }
}
