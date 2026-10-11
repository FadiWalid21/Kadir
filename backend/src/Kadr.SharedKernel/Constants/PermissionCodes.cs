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
    ];
}
