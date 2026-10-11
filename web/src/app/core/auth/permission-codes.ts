/** Mirrors Kadr.SharedKernel.Constants.PermissionCodes for UI checks (never sole security). */
export const PermissionCodes = {
  TenantSettingsRead: 'tenant.settings.read',
  TenantSettingsUpdate: 'tenant.settings.update',
  TenantTemplatesManage: 'tenant.templates.manage',
  UsersRead: 'users.read',
  UsersInvite: 'users.invite',
  UsersUpdate: 'users.update',
  UsersDeactivate: 'users.deactivate',
  RolesRead: 'roles.read',
  RolesManage: 'roles.manage',
  AuditRead: 'audit.read',
  SubscriptionRead: 'subscription.read',
  SubscriptionManage: 'subscription.manage',
  EmployeesRead: 'employees.read',
  EmployeesCreate: 'employees.create',
  EmployeesUpdate: 'employees.update',
  EmployeesTerminate: 'employees.terminate',
  EmployeesDocumentsManage: 'employees.documents.manage',
  EmployeesImport: 'employees.import',
  OrgRead: 'org.read',
  OrgManage: 'org.manage',
} as const;

export type PermissionCode = (typeof PermissionCodes)[keyof typeof PermissionCodes];
