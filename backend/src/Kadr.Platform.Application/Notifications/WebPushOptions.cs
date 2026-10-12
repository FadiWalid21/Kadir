namespace Kadr.Platform.Application.Notifications;

/// <summary>VAPID configuration. The private key comes only from user-secrets, env vars, or Key Vault.</summary>
public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>When true, public/private keys and Subject are required at startup.</summary>
    public bool Enabled { get; set; }

    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;

    /// <summary>Optional host suffixes for push endpoints (e.g. fcm.googleapis.com). Empty = built-in defaults.</summary>
    public string[] AllowedEndpointHosts { get; set; } = [];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(VapidPublicKey);

    public bool CanSend =>
        Enabled
        && !string.IsNullOrWhiteSpace(VapidPublicKey)
        && !string.IsNullOrWhiteSpace(VapidPrivateKey)
        && !string.IsNullOrWhiteSpace(Subject);

    /// <summary>
    /// Null when valid. When Enabled (or a private key is set), public key and mailto:/https: subject are required.
    /// Never includes secret values in the message.
    /// </summary>
    public static string? Validate(WebPushOptions options)
    {
        var requireFull = options.Enabled || !string.IsNullOrWhiteSpace(options.VapidPrivateKey);
        if (!requireFull)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.VapidPublicKey))
        {
            return "WebPush:VapidPublicKey is required when WebPush is enabled or a private key is set.";
        }

        if (string.IsNullOrWhiteSpace(options.VapidPrivateKey))
        {
            return "WebPush:VapidPrivateKey is required when WebPush:Enabled is true.";
        }

        if (string.IsNullOrWhiteSpace(options.Subject)
            || !(options.Subject.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                 || options.Subject.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return "WebPush:Subject must be a mailto: or https: URI when WebPush is enabled.";
        }

        return null;
    }
}
