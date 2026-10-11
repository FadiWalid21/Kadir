using Kadr.Platform.Application.Auth;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IGoogleAuthService
{
    AuthProvidersResponse GetProviders();

    Task<Result<GoogleNonceResponse>> IssueNonceAsync(CancellationToken cancellationToken = default);

    Task<Result<object>> LoginAsync(
        string credential,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result<GoogleLoginSuccessDto>> SelectTenantAsync(
        string selectionToken,
        Guid tenantId,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
