namespace Kadr.Identity.Application.Auth;

public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetPermissionsAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRoleNamesAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IPermissionCacheInvalidator
{
    void Invalidate(Guid tenantId, Guid userId);
}
