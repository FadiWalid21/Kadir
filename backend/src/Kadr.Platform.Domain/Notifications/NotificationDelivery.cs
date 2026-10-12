using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Notifications;

/// <summary>Per-channel delivery row. For Email and Push it is the outbox consumed by the delivery runner.</summary>
public sealed class NotificationDelivery : Entity, ITenantOwned
{
    public const int MaxAttempts = 5;
    public const int MaxErrorLength = 500;

    /// <summary>Delay before the next attempt, indexed by the number of failed attempts so far minus one.</summary>
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(30),
    ];

    private NotificationDelivery()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid NotificationId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    public static NotificationDelivery Pending(
        Guid tenantId,
        Guid notificationId,
        NotificationChannel channel,
        DateTimeOffset now) =>
        new()
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NotificationId = notificationId,
            Channel = channel,
            Status = NotificationDeliveryStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        };

    public static NotificationDelivery Sent(
        Guid tenantId,
        Guid notificationId,
        NotificationChannel channel,
        DateTimeOffset now) =>
        new()
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NotificationId = notificationId,
            Channel = channel,
            Status = NotificationDeliveryStatus.Sent,
            AttemptCount = 1,
            CreatedAt = now,
            SentAt = now,
        };

    public static NotificationDelivery Skipped(
        Guid tenantId,
        Guid notificationId,
        NotificationChannel channel,
        DateTimeOffset now,
        string? reason = null) =>
        new()
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NotificationId = notificationId,
            Channel = channel,
            Status = NotificationDeliveryStatus.Skipped,
            LastError = Truncate(reason),
            CreatedAt = now,
        };

    public void MarkSent(DateTimeOffset now)
    {
        AttemptCount++;
        Status = NotificationDeliveryStatus.Sent;
        SentAt = now;
        NextAttemptAt = null;
        LastError = null;
    }

    public void MarkSkipped(string? reason)
    {
        Status = NotificationDeliveryStatus.Skipped;
        NextAttemptAt = null;
        LastError = Truncate(reason);
    }

    /// <summary>Records a failed attempt: schedules a retry (1m / 5m / 30m) or fails after the fifth attempt.</summary>
    public void RecordFailure(DateTimeOffset now, string? error)
    {
        AttemptCount++;
        LastError = Truncate(error);
        if (AttemptCount >= MaxAttempts)
        {
            Status = NotificationDeliveryStatus.Failed;
            NextAttemptAt = null;
            return;
        }

        Status = NotificationDeliveryStatus.Pending;
        NextAttemptAt = now + Backoff[Math.Min(AttemptCount - 1, Backoff.Length - 1)];
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];
    }
}
