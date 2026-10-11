import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap, throwError, catchError } from 'rxjs';
import { PlatformAuthService } from '../platform/platform-auth.service';
import { AuthService } from './auth.service';

function isPlatformUrl(url: string): boolean {
  return url.includes('/platform/');
}

function isPlatformAuthBootstrapUrl(url: string): boolean {
  return (
    url.includes('/platform/auth/login') ||
    url.includes('/platform/auth/refresh') ||
    url.includes('/platform/auth/logout')
  );
}

function isTenantAuthBootstrapUrl(url: string): boolean {
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
  const withCreds = req.url.startsWith('/api') || req.url.includes('/api/');
  let outgoing = withCreds ? req.clone({ withCredentials: true }) : req;

  if (isPlatformUrl(outgoing.url)) {
    const platformAuth = inject(PlatformAuthService);
    const token = platformAuth.accessToken();
    if (token && !isPlatformAuthBootstrapUrl(outgoing.url)) {
      outgoing = outgoing.clone({
        setHeaders: { Authorization: `Bearer ${token}` },
      });
    }

    return next(outgoing).pipe(
      catchError((error: unknown) => {
        if (
          !(error instanceof HttpErrorResponse) ||
          error.status !== 401 ||
          isPlatformAuthBootstrapUrl(outgoing.url)
        ) {
          return throwError(() => error);
        }

        return from(platformAuth.refreshAccessToken()).pipe(
          switchMap((ok) => {
            if (!ok) {
              void platformAuth.logout();
              return throwError(() => error);
            }
            const retry = outgoing.clone({
              setHeaders: { Authorization: `Bearer ${platformAuth.accessToken()}` },
            });
            return next(retry);
          }),
        );
      }),
    );
  }

  const auth = inject(AuthService);
  const token = auth.accessToken();

  if (token && !isTenantAuthBootstrapUrl(outgoing.url)) {
    outgoing = outgoing.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      if (
        !(error instanceof HttpErrorResponse) ||
        error.status !== 401 ||
        isTenantAuthBootstrapUrl(outgoing.url)
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
