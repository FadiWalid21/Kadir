namespace Kadr.SharedKernel.Tenancy;

/// <summary>
/// Mutable tenant context for registration and tests. JWT wiring uses <see cref="ITenantContext"/> only.
/// </summary>
public interface ITenantContextAccessor : ITenantContext
{
    void Set(Guid tenantId, Guid? userId = null);
    void Clear();
}
