using Kadr.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kadr.Platform.Infrastructure.Email;

/// <summary>Dev/fake sender when Brevo is not configured: logs destination and deep-link path when present.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var pathHint = ExtractPathHint(message.TextBody) ?? ExtractPathHint(message.HtmlBody);
        if (pathHint is not null)
        {
            logger.LogInformation(
                "Email queued to {To} subject {Subject} linkPath {LinkPath}",
                message.To,
                message.Subject,
                pathHint);
        }
        else
        {
            logger.LogInformation("Email queued to {To} subject {Subject}", message.To, message.Subject);
        }

        return Task.CompletedTask;
    }

    /// <summary>Logs only the path+query shape for ops, not a full dump of PII beyond To/Subject.</summary>
    private static string? ExtractPathHint(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        foreach (var marker in new[] { "/accept-invitation?", "/verify-email?", "/reset-password?" })
        {
            var index = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var start = index;
            var end = start;
            while (end < body.Length && !char.IsWhiteSpace(body[end]) && body[end] is not '"' and not '\'')
            {
                end++;
            }

            var slice = body[start..end];
            // Truncate token for log safety while keeping enough to confirm a link was built.
            var q = slice.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
            if (q >= 0 && slice.Length > q + 14)
            {
                return slice[..(q + 14)] + "…";
            }

            return slice.Length > 64 ? slice[..64] + "…" : slice;
        }

        return null;
    }
}
