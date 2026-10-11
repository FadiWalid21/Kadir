import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { FeatureKey } from './feature-keys';
import { SubscriptionStore } from './subscription.store';

export function featureGuard(key: FeatureKey | string): CanActivateFn {
  return async () => {
    const auth = inject(AuthService);
    const store = inject(SubscriptionStore);
    const router = inject(Router);
    await auth.restoreSession();
    if (auth.status() !== 'authenticated') {
      return router.createUrlTree(['/login']);
    }
    if (!store.hasFeature(key)) {
      return router.createUrlTree(['/feature-locked'], { queryParams: { feature: key } });
    }
    return true;
  };
}
