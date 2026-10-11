using Kadr.Platform.Application.Auth;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

/// <summary>
/// Sign-in methods for the authenticated user. Tenant and user come from ITenantContext only.
/// </summary>
public interface ISignInMethodsService
{
    Task<Result<SignInMethodsResponse>> GetAsync(CancellationToken cancellationToken = default);

    Task<Result> LinkGoogleAsync(
        string credential,
        string? refreshOpaqueToken,
        CancellationToken cancellationToken = default);

    Task<Result> UnlinkGoogleAsync(
        string? refreshOpaqueToken,
        CancellationToken cancellationToken = default);

    Task<Result> SetPasswordAsync(
        string? currentPassword,
        string newPassword,
        string? refreshOpaqueToken,
        CancellationToken cancellationToken = default);
}
