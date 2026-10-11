using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Http;

public static class RefreshCookie
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
            AuthConstants.RefreshCookieName,
            opaqueToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = _forceSecure || secure,
                SameSite = _sameSite,
                Path = AuthConstants.RefreshCookiePath,
                Expires = expiresAt.UtcDateTime,
                IsEssential = true,
            });
    }

    public static void Clear(HttpResponse response, bool secure)
    {
        response.Cookies.Delete(
            AuthConstants.RefreshCookieName,
            new CookieOptions
            {
                Path = AuthConstants.RefreshCookiePath,
                Secure = _forceSecure || secure,
                SameSite = _sameSite,
                HttpOnly = true,
            });
    }

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(AuthConstants.RefreshCookieName, out var value) ? value : null;

    public static bool HasCsrfHeader(HttpRequest request) =>
        string.Equals(
            request.Headers[AuthConstants.CsrfHeaderName].FirstOrDefault(),
            AuthConstants.CsrfHeaderValue,
            StringComparison.Ordinal);
}
