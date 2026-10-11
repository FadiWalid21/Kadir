namespace Kadr.Identity.Application.Options;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "Google";

    /// <summary>Public OAuth client ID. Empty disables Google sign-in.</summary>
    public string ClientId { get; set; } = string.Empty;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ClientId);
}
