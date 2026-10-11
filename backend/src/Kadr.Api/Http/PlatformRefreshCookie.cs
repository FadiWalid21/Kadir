using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Http;

public static class PlatformRefreshCookie
{
    public static void Set(HttpResponse response, string opaqueToken, DateTimeOffset expiresAt, bool secure)
    {
        response.Cookies.Append(
            AuthConstants.PlatformRefreshCookieName,
            opaqueToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
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
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                HttpOnly = true,
            });
    }

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(AuthConstants.PlatformRefreshCookieName, out var value) ? value : null;
}
