namespace Kadr.SharedKernel.Constants;

public static class PermissionCodes
{
    public const string TenantSettingsRead = "tenant.settings.read";
    public const string TenantSettingsUpdate = "tenant.settings.update";
    public const string TenantTemplatesManage = "tenant.templates.manage";
    public const string UsersRead = "users.read";
    public const string UsersInvite = "users.invite";
    public const string UsersUpdate = "users.update";
    public const string UsersDeactivate = "users.deactivate";
    public const string RolesRead = "roles.read";
    public const string RolesManage = "roles.manage";
    public const string AuditRead = "audit.read";
    public const string SubscriptionRead = "subscription.read";
    public const string SubscriptionManage = "subscription.manage";
    public const string EmployeesRead = "employees.read";
    public const string EmployeesCreate = "employees.create";
    public const string EmployeesUpdate = "employees.update";
    public const string EmployeesTerminate = "employees.terminate";
    public const string EmployeesDocumentsManage = "employees.documents.manage";
    public const string EmployeesImport = "employees.import";
    public const string OrgRead = "org.read";
    public const string OrgManage = "org.manage";
    public const string AttendancePunch = "attendance.punch";
    public const string AttendanceReadOwn = "attendance.read.own";
    public const string AttendanceReadTeam = "attendance.read.team";
    public const string AttendanceReadAll = "attendance.read.all";
    public const string AttendanceManage = "attendance.manage";
    public const string AttendanceEdit = "attendance.edit";
    public const string AttendanceReports = "attendance.reports";
    public const string LeaveReadOwn = "leave.read.own";
    public const string LeaveRequest = "leave.request";
    public const string LeaveReadTeam = "leave.read.team";
    public const string LeaveReadAll = "leave.read.all";
    public const string LeaveManage = "leave.manage";
    public const string LeaveApprove = "leave.approve";
    public const string WorkflowsManage = "workflows.manage";
    public const string NotificationsRead = "notifications.read";
    public const string NotificationsManagePrefs = "notifications.manage_prefs";
    public const string ReportsRead = "reports.read";
    public const string ReportsExport = "reports.export";
    public const string SalaryRead = "salary.read";
    public const string SalaryManage = "salary.manage";

    public static readonly IReadOnlyList<string> All =
    [
        TenantSettingsRead,
        TenantSettingsUpdate,
        TenantTemplatesManage,
        UsersRead,
        UsersInvite,
        UsersUpdate,
        UsersDeactivate,
        RolesRead,
        RolesManage,
        AuditRead,
        SubscriptionRead,
        SubscriptionManage,
        EmployeesRead,
        EmployeesCreate,
        EmployeesUpdate,
        EmployeesTerminate,
        EmployeesDocumentsManage,
        EmployeesImport,
        OrgRead,
        OrgManage,
        AttendancePunch,
        AttendanceReadOwn,
        AttendanceReadTeam,
        AttendanceReadAll,
        AttendanceManage,
        AttendanceEdit,
        AttendanceReports,
        LeaveReadOwn,
        LeaveRequest,
        LeaveReadTeam,
        LeaveReadAll,
        LeaveManage,
        LeaveApprove,
        WorkflowsManage,
        NotificationsRead,
        NotificationsManagePrefs,
        ReportsRead,
        ReportsExport,
        SalaryRead,
        SalaryManage,
    ];
}
