namespace Kadr.Platform.Application.Abstractions;

/// <summary>Pushes live in-app notification hints to open browser tabs (SignalR). Complementary to Web Push.</summary>
public interface INotificationRealtimeNotifier
{
    Task NotifyCreatedAsync(
        Guid tenantId,
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}
