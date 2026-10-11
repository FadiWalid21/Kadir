using Kadr.Platform.Application.PlatformHealth;

namespace Kadr.Platform.Application.Abstractions;

public interface IPlatformHealthService
{
    Task<PlatformHealthDto> CheckAsync(CancellationToken cancellationToken = default);
}
