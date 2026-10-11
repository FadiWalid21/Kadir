import { Routes } from '@angular/router';
import { authGuard, guestGuard, permissionGuard } from './core/auth/auth.guard';
import { PermissionCodes } from './core/auth/permission-codes';
import { devRoutes } from './dev/dev.routes';

export const routes: Routes = [
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
