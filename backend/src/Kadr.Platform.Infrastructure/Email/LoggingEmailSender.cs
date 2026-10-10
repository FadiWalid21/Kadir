using Kadr.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kadr.Platform.Infrastructure.Email;

/// <summary>Dev/fake sender: logs only (no secrets, no body dump of PII beyond subject/to).</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email queued to {To} subject {Subject}", message.To, message.Subject);
        return Task.CompletedTask;
    }
}
