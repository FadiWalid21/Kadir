using System.Text.RegularExpressions;
using Kadr.SharedKernel.Abstractions;

namespace Kadr.Testing.Support.Email;

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _messages = [];

    public IReadOnlyList<EmailMessage> Messages => _messages;

    public EmailMessage? Last => _messages.LastOrDefault();

    public void Clear() => _messages.Clear();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    public string? ExtractLastVerificationToken() =>
        ExtractTokenFromBodies(
            "verify-email\\?token=",
            "token: ");

    public string? ExtractLastInvitationToken() =>
        ExtractTokenFromBodies(
            "accept-invitation\\?token=",
            "Invitation token: ");

    public string? ExtractLastPasswordResetToken() =>
        ExtractTokenFromBodies(
            "reset-password\\?token=",
            "Reset token: ");

    private string? ExtractTokenFromBodies(string urlPattern, string legacyMarker)
    {
        var body = CombineBodies(Last);
        if (body is null)
        {
            return null;
        }

        var urlMatch = Regex.Match(body, urlPattern + "([^\\s&\"'<>]+)", RegexOptions.IgnoreCase);
        if (urlMatch.Success)
        {
            return Uri.UnescapeDataString(urlMatch.Groups[1].Value);
        }

        var index = body.LastIndexOf(legacyMarker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var start = index + legacyMarker.Length;
        var end = start;
        while (end < body.Length
               && !char.IsWhiteSpace(body[end])
               && body[end] is not '<' and not '"' and not '\'')
        {
            end++;
        }

        return end > start ? body[start..end] : null;
    }

    private static string? CombineBodies(EmailMessage? message)
    {
        if (message is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(message.TextBody) && !string.IsNullOrEmpty(message.HtmlBody))
        {
            return message.TextBody + "\n" + message.HtmlBody;
        }

        return message.TextBody ?? message.HtmlBody;
    }
}
