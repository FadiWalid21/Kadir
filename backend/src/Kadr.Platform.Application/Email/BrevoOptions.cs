namespace Kadr.Platform.Application.Email;

/// <summary>Brevo transactional email. ApiKey comes only from user-secrets, env vars, or Key Vault.</summary>
public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";

    public string ApiKey { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Kadr";

    /// <summary>True when both API key and a sender address are present (ready to send).</summary>
    public bool CanSend =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SenderEmail);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Null when valid. When both ApiKey and SenderEmail are set, SenderEmail must look like an email.
    /// Never includes secret values.
    /// </summary>
    public static string? Validate(BrevoOptions options)
    {
        if (!options.CanSend)
        {
            return null;
        }

        if (!options.SenderEmail.Contains('@', StringComparison.Ordinal))
        {
            return "Brevo:SenderEmail must be a valid email address.";
        }

        return null;
    }
}
