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
        "/api/v1/subscription",
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

        return false;
    }
}
