using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Notifications;
using Kadr.Platform.Domain.Notifications;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kadr.Infrastructure.Persistence.Notifications;

public sealed class PushSubscriptionService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IOptions<WebPushOptions> webPushOptions,
    IPushEndpointValidator endpointValidator,
    IPushNotificationSender pushSender) : IPushSubscriptionService
{
    public Result<VapidPublicKeyDto> GetVapidPublicKey()
    {
        var options = webPushOptions.Value;
        if (!options.Enabled || !options.IsConfigured)
        {
            return Result.Failure<VapidPublicKeyDto>(
                ErrorCodes.NotificationsPushNotConfigured,
                ErrorCodes.NotificationsPushNotConfigured);
        }

        return Result.Success(new VapidPublicKeyDto(options.VapidPublicKey));
    }

    public async Task<Result<PushSubscriptionListDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out _, out var userId))
        {
            return Result.Failure<PushSubscriptionListDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var items = await db.PushSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new PushSubscriptionDeviceDto(
                s.Id,
                s.DeviceLabel,
                s.CreatedAt,
                s.LastSeenAt,
                s.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PushSubscriptionListDto(items));
    }

    public async Task<Result<PushSubscriptionDeviceDto>> UpsertAsync(
        UpsertPushSubscriptionRequest request,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out var tenantId, out var userId))
        {
            return Result.Failure<PushSubscriptionDeviceDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (!webPushOptions.Value.Enabled || !webPushOptions.Value.IsConfigured)
        {
            return Result.Failure<PushSubscriptionDeviceDto>(
                ErrorCodes.NotificationsPushNotConfigured,
                ErrorCodes.NotificationsPushNotConfigured);
        }

        if (request.Keys is null
            || string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.Keys.P256dh)
            || string.IsNullOrWhiteSpace(request.Keys.Auth))
        {
            return Result.Failure<PushSubscriptionDeviceDto>(
                ErrorCodes.NotificationsValidationFailed,
                ErrorCodes.NotificationsValidationFailed);
        }

        var endpointCheck = endpointValidator.Validate(request.Endpoint);
        if (!endpointCheck.IsSuccess)
        {
            return Result.Failure<PushSubscriptionDeviceDto>(endpointCheck.ErrorCode!, endpointCheck.ErrorCode!);
        }

        var endpoint = request.Endpoint.Trim();
        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var activeCount = await db.PushSubscriptions
                .CountAsync(s => s.UserId == userId && s.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (activeCount >= PushSubscription.MaxPerUser)
            {
                return Result.Failure<PushSubscriptionDeviceDto>(
                    ErrorCodes.NotificationsPushLimitExceeded,
                    ErrorCodes.NotificationsPushLimitExceeded);
            }

            existing = PushSubscription.Create(
                tenantId,
                userId,
                endpoint,
                request.Keys.P256dh,
                request.Keys.Auth,
                request.DeviceLabel,
                userAgent);
            db.PushSubscriptions.Add(existing);
        }
        else
        {
            existing.Refresh(request.Keys.P256dh, request.Keys.Auth, request.DeviceLabel, userAgent);
        }

        // Subscribing a device opts the user into push for all known event types so deliveries
        // are not skipped as preference_off while an active subscription exists.
        await EnablePushPreferencesAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToDto(existing));
    }

    private async Task EnablePushPreferencesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var existing = await db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byType = existing.ToDictionary(p => p.EventType, StringComparer.Ordinal);

        foreach (var eventType in NotificationEventTypes.All)
        {
            if (byType.TryGetValue(eventType, out var row))
            {
                if (!row.PushEnabled)
                {
                    row.Update(row.InAppEnabled, row.EmailEnabled, pushEnabled: true);
                }
            }
            else
            {
                db.NotificationPreferences.Add(NotificationPreference.Create(
                    tenantId,
                    userId,
                    eventType,
                    NotificationPreference.DefaultInAppEnabled,
                    NotificationPreference.DefaultEmailEnabled,
                    pushEnabled: true));
            }
        }
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out _, out var userId))
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var deleted = await db.PushSubscriptions
            .Where(s => s.UserId == userId && s.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted > 0
            ? Result.Success()
            : Result.Failure(ErrorCodes.NotificationsPushSubscriptionNotFound, ErrorCodes.NotificationsPushSubscriptionNotFound);
    }

    public async Task<Result> DeleteByEndpointAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out _, out var userId))
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return Result.Failure(ErrorCodes.NotificationsValidationFailed, ErrorCodes.NotificationsValidationFailed);
        }

        await db.PushSubscriptions
            .Where(s => s.UserId == userId && s.Endpoint == endpoint.Trim())
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> SendTestAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out _, out var userId))
        {
            return Result.Failure(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        if (!webPushOptions.Value.Enabled || !webPushOptions.Value.CanSend)
        {
            return Result.Failure(ErrorCodes.NotificationsPushNotConfigured, ErrorCodes.NotificationsPushNotConfigured);
        }

        var subscriptions = await db.PushSubscriptions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (subscriptions.Count == 0)
        {
            return Result.Failure(
                ErrorCodes.NotificationsPushSubscriptionNotFound,
                ErrorCodes.NotificationsPushSubscriptionNotFound);
        }

        var preferredLanguage = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PreferredLanguage)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var arabic = !string.Equals(preferredLanguage, "en", StringComparison.OrdinalIgnoreCase);
        var message = new PushMessage(
            arabic ? "اختبار إشعار كادر" : "Kadr push test",
            arabic ? "هذا إشعار تجريبي من إعداداتك." : "This is a test notification from your settings.",
            "/notifications",
            Tag: "push.test",
            Lang: arabic ? "ar" : "en",
            Dir: arabic ? "rtl" : "ltr");

        var anySent = false;
        foreach (var sub in subscriptions)
        {
            var outcome = await pushSender
                .SendAsync(new PushTarget(sub.Endpoint, sub.P256dh, sub.Auth), message, cancellationToken)
                .ConfigureAwait(false);
            switch (outcome)
            {
                case PushSendOutcome.Sent:
                    anySent = true;
                    sub.TouchSeen();
                    break;
                case PushSendOutcome.SubscriptionGone:
                    db.PushSubscriptions.Remove(sub);
                    break;
                case PushSendOutcome.NotConfigured:
                    return Result.Failure(
                        ErrorCodes.NotificationsPushNotConfigured,
                        ErrorCodes.NotificationsPushNotConfigured);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return anySent
            ? Result.Success()
            : Result.Failure(ErrorCodes.NotificationsValidationFailed, ErrorCodes.NotificationsValidationFailed);
    }

    private bool TryGetUser(out Guid tenantId, out Guid userId)
    {
        tenantId = tenantContext.TenantId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;
        return tenantContext.TenantId is not null && tenantContext.UserId is not null;
    }

    private static PushSubscriptionDeviceDto ToDto(PushSubscription s) =>
        new(s.Id, s.DeviceLabel, s.CreatedAt, s.LastSeenAt, s.IsActive);
}
