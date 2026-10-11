namespace Kadr.Platform.Application.Auth;

/// <summary>Payload stored with a Google registration one-time token. Shared by login and register flows.</summary>
public sealed record GoogleRegistrationPayload(
    string Subject,
    string Email,
    string NormalizedEmail,
    string? Name);

public sealed record SignInMethodsResponse(PasswordMethodDto Password, GoogleMethodDto Google);

public sealed record PasswordMethodDto(bool Enabled);

public sealed record GoogleMethodDto(bool Linked, string? Email);

public sealed record LinkGoogleRequest(string Credential);

public sealed record SetPasswordRequest(string? CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ForgotPasswordResponse(string Status);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public static class ForgotPasswordStatuses
{
    public const string Accepted = "accepted";
}
