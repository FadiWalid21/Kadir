using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kadr.Platform.Application.Email;
using Kadr.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kadr.Platform.Infrastructure.Email;

/// <summary>Sends transactional mail via Brevo HTTP API (no SDK).</summary>
public sealed class BrevoEmailSender(
    IHttpClientFactory httpClientFactory,
    IOptions<BrevoOptions> options,
    ILogger<BrevoEmailSender> logger) : IEmailSender
{
    public const string HttpClientName = nameof(BrevoEmailSender);
    private const int MaxErrorBodyLogChars = 400;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.CanSend)
        {
            throw new InvalidOperationException("Brevo is not configured.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.TryAddWithoutValidation("api-key", opts.ApiKey);
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        request.Content = JsonContent.Create(
            new BrevoSendRequest(
                new BrevoSender(opts.SenderName.Trim(), opts.SenderEmail.Trim()),
                [new BrevoTo(message.To.Trim())],
                message.Subject,
                message.HtmlBody,
                message.TextBody));

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Brevo email accepted for {To} subject {Subject}", message.To, message.Subject);
            return;
        }

        var status = (int)response.StatusCode;
        var errorBody = await ReadErrorBodyAsync(response, cancellationToken).ConfigureAwait(false);
        logger.LogWarning(
            "Brevo email failed for {To} subject {Subject} with status {StatusCode} body {ErrorBody}",
            message.To,
            message.Subject,
            status,
            errorBody);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> ReadErrorBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "(empty)";
            }

            var trimmed = raw.Trim().ReplaceLineEndings(" ");
            return trimmed.Length <= MaxErrorBodyLogChars
                ? trimmed
                : trimmed[..MaxErrorBodyLogChars] + "…";
        }
        catch
        {
            return "(unreadable)";
        }
    }

    private sealed record BrevoSendRequest(
        [property: JsonPropertyName("sender")] BrevoSender Sender,
        [property: JsonPropertyName("to")] IReadOnlyList<BrevoTo> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlContent")] string HtmlContent,
        [property: JsonPropertyName("textContent")] string? TextContent);

    private sealed record BrevoSender(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email);

    private sealed record BrevoTo([property: JsonPropertyName("email")] string Email);
}
