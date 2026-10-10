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

    public string? ExtractLastVerificationToken()
    {
        var body = Last?.HtmlBody;
        if (body is null)
        {
            return null;
        }

        const string marker = "token: ";
        var index = body.LastIndexOf(marker, StringComparison.Ordinal);
        return index < 0 ? null : body[(index + marker.Length)..].Trim();
    }
}
