using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Http;

public static class RefreshCookie
{
    public static void Set(HttpResponse response, string opaqueToken, DateTimeOffset expiresAt, bool secure)
    {
        response.Cookies.Append(
            AuthConstants.RefreshCookieName,
            opaqueToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
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
                Secure = secure,
                SameSite = SameSiteMode.Lax,
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
