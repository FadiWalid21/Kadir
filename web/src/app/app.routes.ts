import { Routes } from '@angular/router';
import { authGuard, guestGuard, permissionGuard } from './core/auth/auth.guard';
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
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.layout').then((m) => m.ShellLayout),
    children: [
      {
        path: '',
        loadComponent: () => import('./home/home.page').then((m) => m.HomePage),
        pathMatch: 'full',
      },
      {
        path: 'account',
        canActivate: [authGuard],
        loadComponent: () => import('./features/account/account.page').then((m) => m.AccountPage),
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
        path: 'settings/company',
        canActivate: [permissionGuard(PermissionCodes.TenantSettingsRead)],
        loadComponent: () =>
          import('./features/settings/company-settings.page').then((m) => m.CompanySettingsPage),
      },
      {
        path: 'settings/templates',
        canActivate: [permissionGuard(PermissionCodes.TenantTemplatesManage)],
        loadComponent: () =>
          import('./features/settings/templates-settings.page').then((m) => m.TemplatesSettingsPage),
      },
      {
        path: 'settings/users',
        canActivate: [permissionGuard(PermissionCodes.UsersRead)],
        loadComponent: () =>
          import('./features/settings/users-settings.page').then((m) => m.UsersSettingsPage),
      },
      {
        path: 'settings/roles',
        canActivate: [permissionGuard(PermissionCodes.RolesRead)],
        loadComponent: () =>
          import('./features/settings/roles-settings.page').then((m) => m.RolesSettingsPage),
      },
      {
        path: 'settings/subscription',
        canActivate: [permissionGuard(PermissionCodes.SubscriptionRead)],
        loadComponent: () =>
          import('./features/settings/subscription-settings.page').then(
            (m) => m.SubscriptionSettingsPage,
          ),
      },
      {
        path: 'settings/audit',
        canActivate: [permissionGuard(PermissionCodes.AuditRead)],
        loadComponent: () =>
          import('./features/settings/audit-settings.page').then((m) => m.AuditSettingsPage),
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
