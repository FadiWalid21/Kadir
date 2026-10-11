using System.Text;
using System.Text.Json;
using Kadr.Identity.Application.Auth;
using Kadr.SharedKernel.Constants;

namespace Kadr.TenantIsolation.Tests.Auth;

/// <summary>
/// Test replacement for the Google ID token validator. The "credential" is an unsigned JWT-shaped string that
/// carries the identity, so no network or Google signing keys are involved. The nonce must match like the real one.
/// </summary>
public sealed class FakeGoogleIdTokenValidator : IGoogleIdTokenValidator
{
    public Task<GoogleIdTokenPayload> ValidateAsync(
        string credential,
        string expectedNonce,
        CancellationToken cancellationToken = default)
    {
        var parts = credential.Split('.');
        if (parts.Length != 3 || parts[2] != "fake-signature")
        {
            throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleTokenInvalid);
        }

        using var doc = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        var root = doc.RootElement;
        if (!string.Equals(root.GetProperty("nonce").GetString(), expectedNonce, StringComparison.Ordinal))
        {
            throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleNonceInvalid);
        }

        var verified = root.GetProperty("email_verified").GetBoolean();
        if (!verified)
        {
            throw new GoogleIdTokenValidationException(ErrorCodes.AuthGoogleEmailUnverified);
        }

        return Task.FromResult(new GoogleIdTokenPayload(
            root.GetProperty("sub").GetString()!,
            root.GetProperty("email").GetString()!,
            root.TryGetProperty("name", out var name) ? name.GetString() : null,
            verified));
    }

    public static string CreateCredential(
        string nonce,
        string subject,
        string email,
        string? name = "Google User",
        bool emailVerified = true)
    {
        var header = Base64UrlEncode(JsonSerializer.Serialize(new { alg = "none", typ = "JWT" }));
        var payload = Base64UrlEncode(JsonSerializer.Serialize(new
        {
            nonce,
            sub = subject,
            email,
            name,
            email_verified = emailVerified,
        }));
        return $"{header}.{payload}.fake-signature";
    }

    private static string Base64UrlEncode(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

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
