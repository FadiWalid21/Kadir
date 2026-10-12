namespace Kadr.Platform.Application.Abstractions;

/// <summary>Processes due Email and Push deliveries with retry and backoff. Never runs inside a DB transaction.</summary>
public interface INotificationDeliveryRunner
{
    /// <summary>Processes up to one batch of due deliveries and returns how many were attempted.</summary>
    Task<int> RunOnceAsync(CancellationToken cancellationToken = default);
}
