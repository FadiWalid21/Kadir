using Kadr.Platform.Application.Abstractions;

namespace Kadr.Infrastructure.Persistence.Notifications;

/// <summary>Default until the API host replaces this with the SignalR notifier.</summary>
public sealed class NoOpNotificationRealtimeNotifier : INotificationRealtimeNotifier
{
    public Task NotifyCreatedAsync(
        Guid tenantId,
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
