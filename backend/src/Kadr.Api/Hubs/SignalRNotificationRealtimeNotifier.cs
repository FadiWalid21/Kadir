using Kadr.Platform.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Kadr.Api.Hubs;

public sealed class SignalRNotificationRealtimeNotifier(IHubContext<NotificationsHub> hubs)
    : INotificationRealtimeNotifier
{
    public Task NotifyCreatedAsync(
        Guid tenantId,
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        return hubs.Clients
            .Group(NotificationsHub.UserGroup(recipientUserId))
            .SendAsync(
                "notificationCreated",
                new { notificationId, unreadDelta = 1 },
                cancellationToken);
    }
}
