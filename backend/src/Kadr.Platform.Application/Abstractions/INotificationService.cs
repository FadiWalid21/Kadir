using Kadr.Platform.Application.Notifications;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

/// <summary>Own-inbox operations. The recipient is always the authenticated user from the tenant context.</summary>
public interface INotificationService
{
    Task<Result<PagedNotificationsDto>> ListAsync(
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    Task<Result<UnreadCountDto>> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task<Result<NotificationDto>> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<MarkAllReadResultDto>> MarkAllReadAsync(CancellationToken cancellationToken = default);

    Task<Result<NotificationPreferencesDto>> GetPreferencesAsync(CancellationToken cancellationToken = default);

    Task<Result<NotificationPreferencesDto>> UpdatePreferencesAsync(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default);
}
