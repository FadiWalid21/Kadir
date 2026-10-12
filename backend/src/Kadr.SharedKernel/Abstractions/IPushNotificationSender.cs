namespace Kadr.SharedKernel.Abstractions;

public sealed record PushMessage(
    string Title,
    string Body,
    string? LinkPath,
    string? Tag = null,
    string Lang = "ar",
    string Dir = "rtl");

public sealed record PushTarget(string Endpoint, string P256dh, string Auth);

public enum PushSendOutcome
{
    Sent = 0,

    /// <summary>The browser reported the subscription as expired or unsubscribed; it should be removed.</summary>
    SubscriptionGone = 1,

    /// <summary>Transient failure; the caller may retry.</summary>
    Failed = 2,

    /// <summary>No push provider or VAPID key pair is configured; nothing was sent.</summary>
    NotConfigured = 3,
}

/// <summary>Web push delivery. Handlers and services never call a push SDK directly.</summary>
public interface IPushNotificationSender
{
    Task<PushSendOutcome> SendAsync(
        PushTarget target,
        PushMessage message,
        CancellationToken cancellationToken = default);
}
