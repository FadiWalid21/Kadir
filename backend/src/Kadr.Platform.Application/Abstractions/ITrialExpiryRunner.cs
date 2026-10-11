namespace Kadr.Platform.Application.Abstractions;

/// <summary>Invokes the trial expiry job once (for tests and the hosted service).</summary>
public interface ITrialExpiryRunner
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}
