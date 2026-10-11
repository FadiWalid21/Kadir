namespace Kadr.SharedKernel.Constants;

public static class AuthConstants
{
    public const int LockoutFailedAttemptThreshold = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromDays(2);
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public const string RefreshCookieName = "kadr_refresh";
    public const string RefreshCookiePath = "/api/v1/auth";
    public const string CsrfHeaderName = "X-Requested-With";
    public const string CsrfHeaderValue = "XMLHttpRequest";
}
