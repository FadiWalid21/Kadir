using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IPasswordResetService
{
    /// <summary>Always succeeds outwardly so callers cannot enumerate accounts.</summary>
    Task<Result> RequestAsync(
        string email,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result> ResetAsync(
        string token,
        string newPassword,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
