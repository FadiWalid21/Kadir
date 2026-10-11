namespace Kadr.SharedKernel.Constants;

public static class AuthConstants
{
    public const int LockoutFailedAttemptThreshold = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromDays(2);
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan GoogleNonceLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan GoogleSelectionLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan GoogleRegistrationLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromMinutes(30);

    public const string RefreshCookieName = "kadr_refresh";
    /// <summary>Wide enough for /auth refresh and /me session-revoke-others (IAM-07).</summary>
    public const string RefreshCookiePath = "/api/v1";
    public const string PlatformRefreshCookieName = "kadr_platform_refresh";
    public const string PlatformRefreshCookiePath = "/api/v1/platform";
    public const string CsrfHeaderName = "X-Requested-With";
    public const string CsrfHeaderValue = "XMLHttpRequest";
}

/// <summary>ASP.NET authorization policy names.</summary>
public static class AuthorizationPolicies
{
    public const string PlatformAdmin = "PlatformAdmin";
}
