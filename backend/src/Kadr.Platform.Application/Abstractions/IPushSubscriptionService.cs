using Kadr.Platform.Application.Notifications;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IPushSubscriptionService
{
    Result<VapidPublicKeyDto> GetVapidPublicKey();

    Task<Result<PushSubscriptionListDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<PushSubscriptionDeviceDto>> UpsertAsync(
        UpsertPushSubscriptionRequest request,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> DeleteByEndpointAsync(string endpoint, CancellationToken cancellationToken = default);

    Task<Result> SendTestAsync(CancellationToken cancellationToken = default);
}