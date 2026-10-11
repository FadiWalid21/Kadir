using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;
using Kadr.SharedKernel.Tenancy;

namespace Kadr.Identity.Domain.Roles;

public sealed class UserRole : Entity, ITenantOwned
{
    private UserRole()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DataScope DataScope { get; private set; }

    public static UserRole Create(Guid tenantId, Guid userId, Guid roleId, DataScope dataScope = DataScope.All)
    {
        return new UserRole
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            RoleId = roleId,
            DataScope = dataScope,
        };
    }

    public void ReplaceRole(Guid roleId, DataScope dataScope)
    {
        RoleId = roleId;
        DataScope = dataScope;
    }
}
