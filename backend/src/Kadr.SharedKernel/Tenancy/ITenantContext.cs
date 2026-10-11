namespace Kadr.SharedKernel.Tenancy;

/// <summary>
/// Current tenant comes only from the authenticated JWT claim (never body/header/query).
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    bool HasTenant { get; }
    /// <summary>True when the request JWT is a platform operator (no tenant_id).</summary>
    bool IsPlatformAdmin { get; }
}
