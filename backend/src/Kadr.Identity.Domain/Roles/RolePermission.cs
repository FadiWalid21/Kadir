using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Identity.Domain.Roles;

public sealed class RolePermission : Entity, ITenantOwned
{
    private RolePermission()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid RoleId { get; private set; }
    public string PermissionCode { get; private set; } = string.Empty;

    public static RolePermission Create(Guid tenantId, Guid roleId, string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        return new RolePermission
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            RoleId = roleId,
            PermissionCode = permissionCode,
        };
    }
}
