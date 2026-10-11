using System.Text;
using System.Text.Json;

namespace Kadr.Infrastructure.Persistence.Auth;

/// <summary>
/// Reads the nonce claim from an unverified Google ID token so the matching one-time nonce can be burned
/// before full signature validation. The value is never trusted on its own.
/// </summary>
internal static class GoogleCredentialReader
{
    public static string? ReadUnverifiedNonce(string credential)
    {
        var parts = credential.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("nonce", out var nonce)
            ? nonce.GetString()
            : null;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
