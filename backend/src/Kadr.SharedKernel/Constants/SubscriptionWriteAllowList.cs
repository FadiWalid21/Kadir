namespace Kadr.SharedKernel.Constants;

/// <summary>Mutating paths allowed when the tenant subscription is read-only.</summary>
public static class SubscriptionWriteAllowList
{
    public static readonly IReadOnlyList<string> Paths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/refresh",
        "/api/v1/auth/logout",
        "/api/v1/auth/accept-invitation",
        "/api/v1/auth/providers",
        "/api/v1/auth/forgot-password",
        "/api/v1/auth/reset-password",
        "/api/v1/subscription",
        "/api/v1/me/sign-in-methods",
        "/api/v1/me/password",
        "/api/v1/me/google",
        "/api/v1/me/google/link",
    ];

    private static readonly string[] Prefixes =
    [
        "/api/v1/auth/google/",
    ];

    public static bool IsAllowed(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (var allowed in Paths)
        {
            if (string.Equals(path, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var prefix in Prefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
