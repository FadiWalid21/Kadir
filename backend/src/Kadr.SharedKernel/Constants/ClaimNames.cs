namespace Kadr.SharedKernel.Constants;

/// <summary>JWT claim names (used from Feature 4 onward).</summary>
public static class ClaimNames
{
    public const string TenantId = "tenant_id";
    public const string Subject = "sub";
    public const string Roles = "roles";
    public const string Language = "lang";
    /// <summary>Present with value "true" on platform-operator JWTs (no tenant_id).</summary>
    public const string PlatformAdmin = "platform_admin";
}
