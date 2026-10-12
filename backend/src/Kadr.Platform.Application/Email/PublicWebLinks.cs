namespace Kadr.Platform.Application.Email;

public static class PublicWebLinks
{
    public const string AcceptInvitationPath = "accept-invitation";
    public const string VerifyEmailPath = "verify-email";
    public const string ResetPasswordPath = "reset-password";

    /// <summary>Returns null when base URL is missing or blank.</summary>
    public static string? TryBuild(string? publicWebBaseUrl, string relativePath, string rawToken)
    {
        if (string.IsNullOrWhiteSpace(publicWebBaseUrl) || string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var baseUrl = publicWebBaseUrl.Trim().TrimEnd('/');
        var path = relativePath.Trim().TrimStart('/');
        return $"{baseUrl}/{path}?token={Uri.EscapeDataString(rawToken)}";
    }
}
