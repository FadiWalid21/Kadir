using Kadr.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kadr.Platform.Infrastructure.Push;

/// <summary>No-op when WebPush is disabled. Logs outcome counts only — never endpoint or keys.</summary>
public sealed class LoggingPushNotificationSender(ILogger<LoggingPushNotificationSender> logger) : IPushNotificationSender
{
    public Task<PushSendOutcome> SendAsync(
        PushTarget target,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Push not configured; skipping title length={TitleLength}", message.Title.Length);
        return Task.FromResult(PushSendOutcome.NotConfigured);
    }
}
