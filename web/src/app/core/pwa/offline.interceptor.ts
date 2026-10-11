import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap, catchError, throwError } from 'rxjs';
import { OfflineService } from './offline.service';

/** Observes network failures without changing 401/403 auth or subscription handling. */
export const offlineInterceptor: HttpInterceptorFn = (req, next) => {
  const offline = inject(OfflineService);
  return next(req).pipe(
    tap((event) => {
      if (event instanceof HttpResponse) {
        offline.noteSuccessfulRequest();
      }
    }),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 0) {
        offline.noteFailedRequest();
      }
      return throwError(() => error);
    }),
  );
};
