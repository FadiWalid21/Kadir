using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Domain.Notifications;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kadr.Infrastructure.Persistence.Notifications;

public sealed class NotificationDeliveryRunner(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationDeliveryRunner> logger) : INotificationDeliveryRunner
{
    private const int BatchSize = 50;
    private const string AppBaseUrlKey = "App:PublicWebBaseUrl";

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KadrDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var pushSender = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();
        var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var configuration = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var appBaseUrl = configuration[AppBaseUrlKey];

        var now = DateTimeOffset.UtcNow;
        var due = await db.NotificationDeliveries
            .IgnoreQueryFilters()
            .Where(d =>
                (d.Channel == NotificationChannel.Email || d.Channel == NotificationChannel.Push)
                && d.Status == NotificationDeliveryStatus.Pending
                && (d.NextAttemptAt == null || d.NextAttemptAt <= now))
            .OrderBy(d => d.NextAttemptAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var attempted = 0;
        foreach (var delivery in due)
        {
            attempted++;
            try
            {
                tenantAccessor.Set(delivery.TenantId, userId: null);
                var notification = await db.Notifications
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(n => n.Id == delivery.NotificationId, cancellationToken)
                    .ConfigureAwait(false);
                if (notification is null)
                {
                    delivery.MarkSkipped("notification_missing");
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (delivery.Channel == NotificationChannel.Email)
                {
                    await SendEmailAsync(db, emailSender, delivery, notification, appBaseUrl, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await SendPushAsync(db, pushSender, delivery, notification, now, logger, cancellationToken)
                        .ConfigureAwait(false);
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Notification delivery {DeliveryId} failed", delivery.Id);
                delivery.RecordFailure(DateTimeOffset.UtcNow, ex.Message);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                tenantAccessor.Clear();
            }
        }

        return attempted;
    }

    private static async Task SendEmailAsync(
        KadrDbContext db,
        IEmailSender emailSender,
        NotificationDelivery delivery,
        Notification notification,
        string? appBaseUrl,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == notification.RecipientUserId)
            .Select(u => new { u.Email, u.PreferredLanguage })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            delivery.MarkSkipped("no_email");
            return;
        }

        var message = NotificationEmailTemplates.Build(
            user.Email,
            notification,
            user.PreferredLanguage,
            appBaseUrl);
        await emailSender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        delivery.MarkSent(now);
    }

    private static async Task SendPushAsync(
        KadrDbContext db,
        IPushNotificationSender pushSender,
        NotificationDelivery delivery,
        Notification notification,
        DateTimeOffset now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var subscriptions = await db.PushSubscriptions.IgnoreQueryFilters()
            .Where(s =>
                s.TenantId == notification.TenantId
                && s.UserId == notification.RecipientUserId
                && s.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (subscriptions.Count == 0)
        {
            delivery.MarkSkipped("no_subscription");
            return;
        }

        var preferredLanguage = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == notification.RecipientUserId)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var message = NotificationPushPayload.Build(notification, preferredLanguage);
        var anySent = false;
        var lastError = (string?)null;
        var sentCount = 0;
        var goneCount = 0;
        var failedCount = 0;
        foreach (var sub in subscriptions)
        {
            var outcome = await pushSender
                .SendAsync(new PushTarget(sub.Endpoint, sub.P256dh, sub.Auth), message, cancellationToken)
                .ConfigureAwait(false);
            switch (outcome)
            {
                case PushSendOutcome.Sent:
                    anySent = true;
                    sentCount++;
                    sub.TouchSeen();
                    break;
                case PushSendOutcome.SubscriptionGone:
                    goneCount++;
                    db.PushSubscriptions.Remove(sub);
                    break;
                case PushSendOutcome.NotConfigured:
                    delivery.MarkSkipped("push_not_configured");
                    return;
                default:
                    failedCount++;
                    lastError = "push_failed";
                    break;
            }
        }

        logger.LogInformation(
            "Push delivery {DeliveryId} tenant={TenantId} user={UserId} sent={Sent} gone={Gone} failed={Failed}",
            delivery.Id,
            notification.TenantId,
            notification.RecipientUserId,
            sentCount,
            goneCount,
            failedCount);

        if (anySent)
        {
            delivery.MarkSent(now);
        }
        else
        {
            delivery.RecordFailure(now, lastError ?? "push_failed");
        }
    }
}

public sealed class NotificationDeliveryHostedService(
    INotificationDeliveryRunner runner,
    IHostEnvironment environment,
    ILogger<NotificationDeliveryHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Notification delivery hosted service disabled in Testing");
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await runner.RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Notification delivery job failed");
            }
        }
    }
}
