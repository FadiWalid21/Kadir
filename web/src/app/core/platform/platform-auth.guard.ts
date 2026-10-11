import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PlatformAuthService } from './platform-auth.service';

export const platformAuthGuard: CanActivateFn = async () => {
  const auth = inject(PlatformAuthService);
  const router = inject(Router);
  await auth.restoreSession();
  if (auth.status() === 'authenticated') {
    return true;
  }
  return router.createUrlTree(['/platform/login']);
};

export const platformGuestGuard: CanActivateFn = async () => {
  const auth = inject(PlatformAuthService);
  const router = inject(Router);
  await auth.restoreSession();
  if (auth.status() === 'authenticated') {
    return router.createUrlTree(['/platform/companies']);
  }
  return true;
};
