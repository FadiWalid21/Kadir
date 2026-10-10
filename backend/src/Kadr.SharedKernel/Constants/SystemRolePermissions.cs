namespace Kadr.SharedKernel.Constants;

public static class SystemRolePermissions
{
    public static IReadOnlyList<string> ForRole(string roleName) => roleName switch
    {
        SystemRoleNames.Owner => PermissionCodes.All,
        SystemRoleNames.HrAdmin => PermissionCodes.All
            .Where(code => code != PermissionCodes.SubscriptionManage)
            .ToArray(),
        SystemRoleNames.Manager =>
        [
            PermissionCodes.TenantSettingsRead,
            PermissionCodes.UsersRead,
            PermissionCodes.RolesRead,
            PermissionCodes.AuditRead,
            PermissionCodes.SubscriptionRead,
        ],
        SystemRoleNames.Employee => [],
        SystemRoleNames.Accountant =>
        [
            PermissionCodes.SubscriptionRead,
            PermissionCodes.AuditRead,
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(roleName), roleName, "Unknown system role."),
    };
}
