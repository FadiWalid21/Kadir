using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Http;

public static class PlatformRefreshCookie
{
    private static SameSiteMode _sameSite = SameSiteMode.Lax;
    private static bool _forceSecure;

    public static void Configure(bool crossSiteCookies)
    {
        if (crossSiteCookies)
        {
            _sameSite = SameSiteMode.None;
            _forceSecure = true;
        }
        else
        {
            _sameSite = SameSiteMode.Lax;
            _forceSecure = false;
        }
    }

    public static void Set(HttpResponse response, string opaqueToken, DateTimeOffset expiresAt, bool secure)
    {
        response.Cookies.Append(
            AuthConstants.PlatformRefreshCookieName,
            opaqueToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = _forceSecure || secure,
                SameSite = _sameSite,
                Path = AuthConstants.PlatformRefreshCookiePath,
                Expires = expiresAt.UtcDateTime,
                IsEssential = true,
            });
    }

    public static void Clear(HttpResponse response, bool secure)
    {
        response.Cookies.Delete(
            AuthConstants.PlatformRefreshCookieName,
            new CookieOptions
            {
                Path = AuthConstants.PlatformRefreshCookiePath,
                Secure = _forceSecure || secure,
                SameSite = _sameSite,
                HttpOnly = true,
            });
    }

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(AuthConstants.PlatformRefreshCookieName, out var value) ? value : null;
}
