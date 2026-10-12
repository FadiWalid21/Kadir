namespace Kadr.Platform.Application.Email;

/// <summary>Public SPA origin used in email deep links (invite, verify, reset).</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>e.g. https://kadir-teal.vercel.app or http://localhost:4200 — no trailing slash.</summary>
    public string PublicWebBaseUrl { get; set; } = string.Empty;
}
