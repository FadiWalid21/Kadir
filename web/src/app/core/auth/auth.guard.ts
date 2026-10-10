import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { PermissionCode } from './permission-codes';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.restoreSession();
  if (auth.status() === 'authenticated') {
    return true;
  }
  return router.createUrlTree(['/login']);
};

export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.restoreSession();
  if (auth.status() === 'authenticated') {
    return router.createUrlTree(['/']);
  }
  return true;
};

export function permissionGuard(code: PermissionCode | string): CanActivateFn {
  return async () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    await auth.restoreSession();
    if (auth.status() !== 'authenticated') {
      return router.createUrlTree(['/login']);
    }
    if (!auth.hasPermission(code)) {
      return router.createUrlTree(['/forbidden']);
    }
    return true;
  };
}
