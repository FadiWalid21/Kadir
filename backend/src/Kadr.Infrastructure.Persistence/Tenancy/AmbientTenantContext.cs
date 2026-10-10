using Kadr.SharedKernel.Tenancy;

namespace Kadr.Infrastructure.Persistence.Tenancy;

/// <summary>
/// Mutable tenant context for the request (or tests). JWT wiring arrives in Feature 4.
/// </summary>
public sealed class AmbientTenantContext : ITenantContextAccessor
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public bool HasTenant => TenantId.HasValue;

    public void Set(Guid tenantId, Guid? userId = null)
    {
        TenantId = tenantId;
        UserId = userId;
    }

    public void Clear()
    {
        TenantId = null;
        UserId = null;
    }
}
