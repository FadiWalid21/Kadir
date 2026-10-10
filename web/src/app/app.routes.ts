import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';
import { devRoutes } from './dev/dev.routes';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'verify-email',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/verify-email.page').then((m) => m.VerifyEmailPage),
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
    ],
  },
  ...devRoutes,
  { path: '**', redirectTo: '' },
];
