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
            PermissionCodes.EmployeesRead,
            PermissionCodes.OrgRead,
            PermissionCodes.AttendancePunch,
            PermissionCodes.AttendanceReadOwn,
            PermissionCodes.AttendanceReadTeam,
            PermissionCodes.AttendanceEdit,
            PermissionCodes.LeaveReadOwn,
            PermissionCodes.LeaveRequest,
            PermissionCodes.LeaveReadTeam,
            PermissionCodes.LeaveApprove,
            PermissionCodes.NotificationsRead,
            PermissionCodes.NotificationsManagePrefs,
            PermissionCodes.ReportsRead,
        ],
        SystemRoleNames.Employee =>
        [
            PermissionCodes.EmployeesRead,
            PermissionCodes.AttendancePunch,
            PermissionCodes.AttendanceReadOwn,
            PermissionCodes.LeaveReadOwn,
            PermissionCodes.LeaveRequest,
            PermissionCodes.NotificationsRead,
            PermissionCodes.NotificationsManagePrefs,
        ],
        SystemRoleNames.Accountant =>
        [
            PermissionCodes.SubscriptionRead,
            PermissionCodes.AuditRead,
            PermissionCodes.NotificationsRead,
            PermissionCodes.NotificationsManagePrefs,
            PermissionCodes.ReportsRead,
            PermissionCodes.ReportsExport,
            PermissionCodes.PayrollRulesRead,
            PermissionCodes.PayrollRun,
            PermissionCodes.PayrollApprove,
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(roleName), roleName, "Unknown system role."),
    };
}
