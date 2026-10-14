using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Domain.Notifications;
using Kadr.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Notifications;

public sealed class ApprovalRequiredNotificationHandler(
    KadrDbContext db,
    INotificationRealtimeNotifier realtime)
    : IDomainEventHandler<ApprovalRequiredNotificationRequested>
{
    public Task HandleAsync(
        ApprovalRequiredNotificationRequested domainEvent,
        CancellationToken cancellationToken = default) =>
        NotificationCreator.CreateAsync(
            db,
            realtime,
            domainEvent.TenantId,
            domainEvent.RecipientUserId,
            NotificationContentBuilder.ApprovalRequired(
                domainEvent.RequestType,
                domainEvent.RequesterNameAr,
                domainEvent.RequesterNameEn),
            domainEvent.LinkPath,
            JsonSerializer.Serialize(new
            {
                domainEvent.InstanceId,
                domainEvent.SubjectId,
                requestType = domainEvent.RequestType.ToString(),
            }),
            cancellationToken);
}

public sealed class RequestDecisionNotificationHandler(
    KadrDbContext db,
    INotificationRealtimeNotifier realtime)
    : IDomainEventHandler<RequestDecisionNotificationRequested>
{
    public Task HandleAsync(
        RequestDecisionNotificationRequested domainEvent,
        CancellationToken cancellationToken = default) =>
        NotificationCreator.CreateAsync(
            db,
            realtime,
            domainEvent.TenantId,
            domainEvent.RecipientUserId,
            NotificationContentBuilder.Decision(
                domainEvent.RequestType,
                domainEvent.Approved,
                domainEvent.RejectReason),
            domainEvent.LinkPath,
            JsonSerializer.Serialize(new
            {
                domainEvent.InstanceId,
                domainEvent.SubjectId,
                approved = domainEvent.Approved,
                requestType = domainEvent.RequestType.ToString(),
            }),
            cancellationToken);
}

internal static class NotificationCreator
{
    public static async Task CreateAsync(
        KadrDbContext db,
        INotificationRealtimeNotifier realtime,
        Guid tenantId,
        Guid recipientUserId,
        NotificationContent content,
        string? linkPath,
        string? metadataJson,
        CancellationToken cancellationToken)
    {
        if (recipientUserId == Guid.Empty)
        {
            return;
        }

        var prefs = await db.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.UserId == recipientUserId && p.EventType == content.Type,
                cancellationToken)
            .ConfigureAwait(false);

        var inApp = prefs?.InAppEnabled ?? NotificationPreference.DefaultInAppEnabled;
        var email = prefs?.EmailEnabled ?? NotificationPreference.DefaultEmailEnabled;
        var push = prefs?.PushEnabled ?? NotificationPreference.DefaultPushEnabled;

        var now = DateTimeOffset.UtcNow;
        var notification = Notification.Create(
            tenantId,
            recipientUserId,
            content.Type,
            content.TitleAr,
            content.TitleEn,
            content.BodyAr,
            content.BodyEn,
            linkPath,
            metadataJson,
            inboxVisible: inApp);
        db.Notifications.Add(notification);

        db.NotificationDeliveries.Add(
            inApp
                ? NotificationDelivery.Sent(tenantId, notification.Id, NotificationChannel.InApp, now)
                : NotificationDelivery.Skipped(tenantId, notification.Id, NotificationChannel.InApp, now, "preference_off"));

        db.NotificationDeliveries.Add(
            email
                ? NotificationDelivery.Pending(tenantId, notification.Id, NotificationChannel.Email, now)
                : NotificationDelivery.Skipped(tenantId, notification.Id, NotificationChannel.Email, now, "preference_off"));

        if (push)
        {
            var hasSubscription = await db.PushSubscriptions.AsNoTracking()
                .AnyAsync(
                    s => s.TenantId == tenantId && s.UserId == recipientUserId && s.IsActive,
                    cancellationToken)
                .ConfigureAwait(false);
            db.NotificationDeliveries.Add(
                hasSubscription
                    ? NotificationDelivery.Pending(tenantId, notification.Id, NotificationChannel.Push, now)
                    : NotificationDelivery.Skipped(
                        tenantId,
                        notification.Id,
                        NotificationChannel.Push,
                        now,
                        "no_subscription"));
        }
        else
        {
            db.NotificationDeliveries.Add(
                NotificationDelivery.Skipped(tenantId, notification.Id, NotificationChannel.Push, now, "preference_off"));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (inApp)
        {
            await realtime
                .NotifyCreatedAsync(tenantId, recipientUserId, notification.Id, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
