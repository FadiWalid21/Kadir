import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { catchError, throwError } from 'rxjs';
import { problemDetailsFromError } from '../api/auth.api';
import { SubscriptionApi } from '../api/subscription.api';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { SubscriptionStore } from './subscription.store';

export const subscriptionErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);
  const store = inject(SubscriptionStore);
  const api = inject(SubscriptionApi);
  const transloco = inject(TranslocoService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 403) {
        return throwError(() => error);
      }

      const code = problemDetailsFromError(error)?.code;
      if (code === 'subscription.expired') {
        toast.show(
          transloco.translate('subscription.errors.expired'),
          'danger',
          8000,
          {
            label: transloco.translate('subscription.errors.viewPlans'),
            url: '/settings/subscription',
          },
        );
      } else if (code === 'plan.employee_cap_exceeded') {
        void api
          .getSubscription()
          .then((dto) => store.openSeatLimitDialog(dto.usage.cap, dto.usage.activeEmployees))
          .catch(() => store.openSeatLimitDialog(null, 0));
      } else if (code === 'plan.feature_not_available') {
        toast.show(transloco.translate('subscription.errors.featureLocked'), 'danger');
      }

      return throwError(() => error);
    }),
  );
};
