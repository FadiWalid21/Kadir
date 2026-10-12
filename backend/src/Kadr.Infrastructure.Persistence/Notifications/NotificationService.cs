using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Domain.Notifications;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Notifications;

public sealed class NotificationService(
    KadrDbContext db,
    ITenantContext tenantContext) : INotificationService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<Result<PagedNotificationsDto>> ListAsync(
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out var tenantId, out var userId))
        {
            return Result.Failure<PagedNotificationsDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var query = db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == userId && n.InboxVisible);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var unread = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && n.InboxVisible && n.ReadAt == null, cancellationToken)
            .ConfigureAwait(false);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type,
                n.TitleAr,
                n.TitleEn,
                n.BodyAr,
                n.BodyEn,
                n.LinkPath,
                n.MetadataJson,
                n.CreatedAt,
                n.ReadAt,
                n.ReadAt != null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _ = tenantId;
        return Result.Success(new PagedNotificationsDto(items, page, pageSize, total, unread));
    }

    public async Task<Result<UnreadCountDto>> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out _, out var userId))
        {
            return Result.Failure<UnreadCountDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var count = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && n.InboxVisible && n.ReadAt == null, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(new UnreadCountDto(count));
    }

    public async Task<Result<NotificationDto>> MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out _, out var userId))
        {
            return Result.Failure<NotificationDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == userId && n.InboxVisible, cancellationToken)
            .ConfigureAwait(false);
        if (notification is null)
        {
            return Result.Failure<NotificationDto>(ErrorCodes.NotificationsNotFound, ErrorCodes.NotificationsNotFound);
        }

        notification.MarkRead();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToDto(notification));
    }

    public async Task<Result<MarkAllReadResultDto>> MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out _, out var userId))
        {
            return Result.Failure<MarkAllReadResultDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var now = DateTimeOffset.UtcNow;
        var updated = await db.Notifications
            .Where(n => n.RecipientUserId == userId && n.InboxVisible && n.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(n => n.ReadAt, now),
                cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(new MarkAllReadResultDto(updated));
    }

    public async Task<Result<NotificationPreferencesDto>> GetPreferencesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out var tenantId, out var userId))
        {
            return Result.Failure<NotificationPreferencesDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var stored = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byType = stored.ToDictionary(p => p.EventType, StringComparer.Ordinal);
        var items = NotificationEventTypes.All
            .Select(type => byType.TryGetValue(type, out var row)
                ? new NotificationPreferenceDto(type, row.InAppEnabled, row.EmailEnabled, row.PushEnabled)
                : new NotificationPreferenceDto(
                    type,
                    NotificationPreference.DefaultInAppEnabled,
                    NotificationPreference.DefaultEmailEnabled,
                    NotificationPreference.DefaultPushEnabled))
            .ToList();
        _ = tenantId;
        return Result.Success(new NotificationPreferencesDto(items));
    }

    public async Task<Result<NotificationPreferencesDto>> UpdatePreferencesAsync(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out var tenantId, out var userId))
        {
            return Result.Failure<NotificationPreferencesDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (request.Items is null
            || request.Items.Count == 0
            || request.Items.Any(i => !NotificationEventTypes.IsKnown(i.EventType))
            || request.Items.Select(i => i.EventType).Distinct(StringComparer.Ordinal).Count() != request.Items.Count)
        {
            return Result.Failure<NotificationPreferencesDto>(
                ErrorCodes.NotificationsValidationFailed,
                ErrorCodes.NotificationsValidationFailed);
        }

        var existing = await db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byType = existing.ToDictionary(p => p.EventType, StringComparer.Ordinal);

        foreach (var item in request.Items)
        {
            if (byType.TryGetValue(item.EventType, out var row))
            {
                row.Update(item.InAppEnabled, item.EmailEnabled, item.PushEnabled);
            }
            else
            {
                db.NotificationPreferences.Add(NotificationPreference.Create(
                    tenantId,
                    userId,
                    item.EventType,
                    item.InAppEnabled,
                    item.EmailEnabled,
                    item.PushEnabled));
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetPreferencesAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool TryGetUser(out Guid tenantId, out Guid userId)
    {
        tenantId = tenantContext.TenantId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;
        return tenantContext.TenantId is not null && tenantContext.UserId is not null;
    }

    private static NotificationDto ToDto(Notification n) =>
        new(
            n.Id,
            n.Type,
            n.TitleAr,
            n.TitleEn,
            n.BodyAr,
            n.BodyEn,
            n.LinkPath,
            n.MetadataJson,
            n.CreatedAt,
            n.ReadAt,
            n.IsRead);
}
