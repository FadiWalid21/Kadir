using Kadr.SharedKernel.Tenancy;

namespace Kadr.Infrastructure.Persistence.PlatformTenants;

/// <summary>
/// Lets a platform admin write to one target tenant for the duration of a single audited operation.
/// The tenant id is chosen by the operator (route id) and only ever used inside platform-admin code that has
/// already passed the PlatformAdmin policy; the actor stays the platform admin. The previous platform context is
/// restored on dispose so later work in the same request can never run with a tenant left behind.
/// </summary>
internal sealed class PlatformTenantScope : IDisposable
{
    private readonly ITenantContextAccessor _accessor;
    private readonly Guid _adminId;

    private PlatformTenantScope(ITenantContextAccessor accessor, Guid adminId)
    {
        _accessor = accessor;
        _adminId = adminId;
    }

    public static PlatformTenantScope Enter(ITenantContextAccessor accessor, Guid tenantId)
    {
        if (!accessor.IsPlatformAdmin || accessor.UserId is not { } adminId)
        {
            throw new InvalidOperationException("A platform admin context is required.");
        }

        accessor.Set(tenantId, adminId);
        return new PlatformTenantScope(accessor, adminId);
    }

    public void Dispose() => _accessor.SetPlatformAdmin(_adminId);
}
