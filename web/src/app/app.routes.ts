import { Routes } from '@angular/router';
import { authGuard, guestGuard, landingGuard, permissionGuard } from './core/auth/auth.guard';
import { PermissionCodes } from './core/auth/permission-codes';
import { platformAuthGuard, platformGuestGuard } from './core/platform/platform-auth.guard';
import { featureGuard } from './core/subscription/feature.guard';
import { FeatureKeys } from './core/subscription/feature-keys';
import { devRoutes } from './dev/dev.routes';

export const routes: Routes = [
  {
    path: 'platform/login',
    canActivate: [platformGuestGuard],
    loadComponent: () =>
      import('./features/platform/platform-login.page').then((m) => m.PlatformLoginPage),
  },
  {
    path: 'platform',
    canActivate: [platformAuthGuard],
    loadComponent: () =>
      import('./features/platform/platform-shell.layout').then((m) => m.PlatformShellLayout),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'companies' },
      {
        path: 'companies',
        loadComponent: () =>
          import('./features/platform/platform-companies.page').then((m) => m.PlatformCompaniesPage),
      },
      {
        path: 'companies/:id',
        loadComponent: () =>
          import('./features/platform/platform-company-detail.page').then(
            (m) => m.PlatformCompanyDetailPage,
          ),
      },
      {
        path: 'plans',
        loadComponent: () =>
          import('./features/platform/platform-plans.page').then((m) => m.PlatformPlansPage),
      },
      {
        path: 'audit',
        loadComponent: () =>
          import('./features/platform/platform-audit.page').then((m) => m.PlatformAuditPage),
      },
      {
        path: 'health',
        loadComponent: () =>
          import('./features/platform/platform-health.page').then((m) => m.PlatformHealthPage),
      },
    ],
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'login/choose-company',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/choose-company.page').then((m) => m.ChooseCompanyPage),
  },
  {
    path: 'forgot-password',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/forgot-password.page').then((m) => m.ForgotPasswordPage),
  },
  {
    // Public on purpose: a signed-in user can still open a reset link from their email.
    path: 'reset-password',
    loadComponent: () =>
      import('./features/auth/reset-password.page').then((m) => m.ResetPasswordPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/onboarding/onboarding.page').then((m) => m.OnboardingPage),
  },
  {
    path: 'verify-email',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/verify-email.page').then((m) => m.VerifyEmailPage),
  },
  {
    path: 'accept-invitation',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/accept-invitation.page').then((m) => m.AcceptInvitationPage),
  },
  {
    path: 'forbidden',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/auth/forbidden.page').then((m) => m.ForbiddenPage),
  },
  {
    path: '',
    pathMatch: 'full',
    canActivate: [landingGuard],
    loadComponent: () =>
      import('./features/landing/landing.page').then((m) => m.LandingPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.layout').then((m) => m.ShellLayout),
    children: [
      {
        path: 'home',
        loadComponent: () => import('./home/home.page').then((m) => m.HomePage),
      },
      {
        path: 'account',
        canActivate: [authGuard],
        loadComponent: () => import('./features/account/account.page').then((m) => m.AccountPage),
      },
      {
        path: 'account/notifications',
        canActivate: [authGuard, permissionGuard(PermissionCodes.NotificationsManagePrefs)],
        loadComponent: () =>
          import('./features/notifications/notification-preferences.page').then(
            (m) => m.NotificationPreferencesPage,
          ),
      },
      {
        path: 'notifications',
        canActivate: [authGuard, permissionGuard(PermissionCodes.NotificationsRead)],
        loadComponent: () =>
          import('./features/notifications/notifications.page').then((m) => m.NotificationsPage),
      },
      {
        path: 'employees',
        canActivate: [
          permissionGuard(PermissionCodes.EmployeesRead),
          featureGuard(FeatureKeys.CoreHr),
        ],
        loadComponent: () =>
          import('./features/employees/employees-list.page').then((m) => m.EmployeesListPage),
      },
      {
        path: 'employees/new',
        canActivate: [
          permissionGuard(PermissionCodes.EmployeesCreate),
          featureGuard(FeatureKeys.CoreHr),
        ],
        loadComponent: () =>
          import('./features/employees/employee-form.page').then((m) => m.EmployeeFormPage),
      },
      {
        path: 'employees/import',
        canActivate: [
          permissionGuard(PermissionCodes.EmployeesImport),
          featureGuard(FeatureKeys.CoreHr),
        ],
        loadComponent: () =>
          import('./features/employees/employees-import.page').then((m) => m.EmployeesImportPage),
      },
      {
        path: 'employees/salaries/import',
        canActivate: [
          permissionGuard(PermissionCodes.SalaryManage),
          featureGuard(FeatureKeys.CoreHr),
        ],
        loadComponent: () =>
          import('./features/employees/salaries-import.page').then((m) => m.SalariesImportPage),
      },
      {
        path: 'employees/:id/edit',
        canActivate: [
          permissionGuard(PermissionCodes.EmployeesUpdate),
          featureGuard(FeatureKeys.CoreHr),
        ],
        loadComponent: () =>
          import('./features/employees/employee-form.page').then((m) => m.EmployeeFormPage),
      },
      {
        path: 'employees/:id',
        canActivate: [
          permissionGuard(PermissionCodes.EmployeesRead),
          featureGuard(FeatureKeys.CoreHr),
        ],
        loadComponent: () =>
          import('./features/employees/employee-profile.page').then((m) => m.EmployeeProfilePage),
      },
      {
        path: 'documents/expiring',
        canActivate: [
          permissionGuard(PermissionCodes.EmployeesRead),
          featureGuard(FeatureKeys.CoreHr),
        ],
        loadComponent: () =>
          import('./features/employees/documents-expiring.page').then(
            (m) => m.DocumentsExpiringPage,
          ),
      },
      {
        path: 'org',
        canActivate: [
          permissionGuard(PermissionCodes.OrgRead),
          featureGuard(FeatureKeys.CoreHr),
        ],
        loadComponent: () =>
          import('./features/employees/org-tree.page').then((m) => m.OrgTreePage),
      },
      {
        path: 'attendance/today',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceReadTeam),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/attendance-today.page').then((m) => m.AttendanceTodayPage),
      },
      {
        path: 'attendance/schedules/rules',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceManage),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/shift-rules.page').then((m) => m.ShiftRulesPage),
      },
      {
        path: 'attendance/schedules',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceManage),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/attendance-schedules.page').then(
            (m) => m.AttendanceSchedulesPage,
          ),
      },
      {
        path: 'attendance/reports',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceReports),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/attendance-reports.page').then(
            (m) => m.AttendanceReportsPage,
          ),
      },
      {
        path: 'attendance/locations',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceManage),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/work-locations-list.page').then(
            (m) => m.WorkLocationsListPage,
          ),
      },
      {
        path: 'attendance/locations/:id/assign',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceManage),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/work-location-assign.page').then(
            (m) => m.WorkLocationAssignPage,
          ),
      },
      {
        path: 'attendance/corrections',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceReadTeam),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/corrections-inbox.page').then(
            (m) => m.CorrectionsInboxPage,
          ),
      },
      {
        path: 'attendance/me',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceReadOwn),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/my-month.page').then((m) => m.MyMonthPage),
      },
      {
        path: 'attendance/requests',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceReadOwn),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/my-requests.page').then((m) => m.MyRequestsPage),
      },
      {
        path: 'attendance/settings/rules',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceManage),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/settings/attendance-rules.page').then(
            (m) => m.AttendanceRulesPage,
          ),
      },
      {
        path: 'attendance/settings/branches',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceManage),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/settings/attendance-branches.page').then(
            (m) => m.AttendanceBranchesPage,
          ),
      },
      {
        path: 'attendance/settings/holidays',
        canActivate: [
          permissionGuard(PermissionCodes.AttendanceManage),
          featureGuard(FeatureKeys.Attendance),
        ],
        loadComponent: () =>
          import('./features/attendance/settings/attendance-holidays.page').then(
            (m) => m.AttendanceHolidaysPage,
          ),
      },
      {
        path: 'leave/me',
        canActivate: [
          permissionGuard(PermissionCodes.LeaveReadOwn),
          featureGuard(FeatureKeys.Leave),
        ],
        loadComponent: () => import('./features/leave/leave-me.page').then((m) => m.LeaveMePage),
      },
      {
        path: 'leave/balances',
        canActivate: [
          permissionGuard(PermissionCodes.LeaveReadAll),
          featureGuard(FeatureKeys.Leave),
        ],
        loadComponent: () =>
          import('./features/leave/leave-balances.page').then((m) => m.LeaveBalancesPage),
      },
      {
        path: 'leave/approvals',
        canActivate: [
          permissionGuard(PermissionCodes.LeaveReadTeam),
          featureGuard(FeatureKeys.Leave),
        ],
        loadComponent: () =>
          import('./features/leave/leave-approvals.page').then((m) => m.LeaveApprovalsPage),
      },
      {
        path: 'reports',
        canActivate: [
          permissionGuard(PermissionCodes.ReportsRead),
          featureGuard(FeatureKeys.Reports),
        ],
        loadComponent: () =>
          import('./features/reports/reports-hub.page').then((m) => m.ReportsHubPage),
      },
      {
        path: 'reports/headcount',
        canActivate: [
          permissionGuard(PermissionCodes.ReportsRead),
          featureGuard(FeatureKeys.Reports),
        ],
        loadComponent: () =>
          import('./features/reports/headcount-report.page').then((m) => m.HeadcountReportPage),
      },
      {
        path: 'payroll/runs',
        canActivate: [permissionGuard(PermissionCodes.PayrollRun)],
        loadComponent: () =>
          import('./features/payroll/payroll-runs.page').then((m) => m.PayrollRunsPage),
      },
      {
        path: 'payroll/runs/:id',
        canActivate: [permissionGuard(PermissionCodes.PayrollRun)],
        loadComponent: () =>
          import('./features/payroll/payroll-run-detail.page').then((m) => m.PayrollRunDetailPage),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/settings/settings.layout').then((m) => m.SettingsLayout),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'company' },
          {
            path: 'company',
            canActivate: [permissionGuard(PermissionCodes.TenantSettingsRead)],
            loadComponent: () =>
              import('./features/settings/company-settings.page').then((m) => m.CompanySettingsPage),
          },
          {
            path: 'company/branches',
            canActivate: [permissionGuard(PermissionCodes.TenantSettingsRead)],
            loadComponent: () =>
              import('./features/settings/branches-settings.page').then(
                (m) => m.BranchesSettingsPage,
              ),
          },
          {
            path: 'leave-types',
            canActivate: [
              permissionGuard(PermissionCodes.LeaveManage),
              featureGuard(FeatureKeys.Leave),
            ],
            loadComponent: () =>
              import('./features/settings/leave-types-settings.page').then(
                (m) => m.LeaveTypesSettingsPage,
              ),
          },
          {
            path: 'pay-components',
            canActivate: [permissionGuard(PermissionCodes.SalaryRead)],
            loadComponent: () =>
              import('./features/settings/pay-components-settings.page').then(
                (m) => m.PayComponentsSettingsPage,
              ),
          },
          {
            path: 'payroll-rules',
            canActivate: [permissionGuard(PermissionCodes.PayrollRulesRead)],
            loadComponent: () =>
              import('./features/settings/payroll-rules-settings.page').then(
                (m) => m.PayrollRulesSettingsPage,
              ),
          },
          {
            path: 'workflows',
            canActivate: [
              permissionGuard(PermissionCodes.WorkflowsManage),
              featureGuard(FeatureKeys.Workflows),
            ],
            loadComponent: () =>
              import('./features/settings/workflows-settings.page').then(
                (m) => m.WorkflowsSettingsPage,
              ),
          },
          {
            path: 'templates',
            canActivate: [permissionGuard(PermissionCodes.TenantTemplatesManage)],
            loadComponent: () =>
              import('./features/settings/templates-settings.page').then(
                (m) => m.TemplatesSettingsPage,
              ),
          },
          {
            path: 'users',
            canActivate: [permissionGuard(PermissionCodes.UsersRead)],
            loadComponent: () =>
              import('./features/settings/users-settings.page').then((m) => m.UsersSettingsPage),
          },
          {
            path: 'invitations',
            canActivate: [permissionGuard(PermissionCodes.UsersInvite)],
            loadComponent: () =>
              import('./features/settings/invitations-settings.page').then(
                (m) => m.InvitationsSettingsPage,
              ),
          },
          {
            path: 'roles',
            canActivate: [permissionGuard(PermissionCodes.RolesRead)],
            loadComponent: () =>
              import('./features/settings/roles-settings.page').then((m) => m.RolesSettingsPage),
          },
          {
            path: 'subscription',
            canActivate: [permissionGuard(PermissionCodes.SubscriptionRead)],
            loadComponent: () =>
              import('./features/settings/subscription-settings.page').then(
                (m) => m.SubscriptionSettingsPage,
              ),
          },
          {
            path: 'audit',
            canActivate: [permissionGuard(PermissionCodes.AuditRead)],
            loadComponent: () =>
              import('./features/settings/audit-settings.page').then((m) => m.AuditSettingsPage),
          },
        ],
      },
      {
        path: 'feature-locked',
        loadComponent: () =>
          import('./features/subscription/feature-locked.page').then((m) => m.FeatureLockedPage),
      },
    ],
  },
  ...devRoutes,
  { path: '**', redirectTo: '' },
];
