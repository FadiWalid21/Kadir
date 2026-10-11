import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap, throwError, catchError } from 'rxjs';
import { AuthService } from './auth.service';

function isAuthBootstrapUrl(url: string): boolean {
  return (
    url.includes('/auth/login') ||
    url.includes('/auth/refresh') ||
    url.includes('/auth/logout') ||
    url.includes('/auth/register') ||
    url.includes('/auth/verify-email') ||
    url.includes('/auth/providers') ||
    url.includes('/auth/google/') ||
    url.includes('/auth/forgot-password') ||
    url.includes('/auth/reset-password')
  );
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();

  const withCreds = req.url.startsWith('/api') || req.url.includes('/api/');
  let outgoing = withCreds ? req.clone({ withCredentials: true }) : req;

  if (token && !isAuthBootstrapUrl(outgoing.url)) {
    outgoing = outgoing.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      if (
        !(error instanceof HttpErrorResponse) ||
        error.status !== 401 ||
        isAuthBootstrapUrl(outgoing.url)
      ) {
        return throwError(() => error);
      }

      return from(auth.refreshAccessToken()).pipe(
        switchMap((ok) => {
          if (!ok) {
            void auth.logout();
            return throwError(() => error);
          }
          const retry = outgoing.clone({
            setHeaders: { Authorization: `Bearer ${auth.accessToken()}` },
          });
          return next(retry);
        }),
      );
    }),
  );
};
