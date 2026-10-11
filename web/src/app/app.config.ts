import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  APP_INITIALIZER,
  isDevMode,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { TranslocoHttpLoader } from './core/i18n/transloco-loader';
import { offlineInterceptor } from './core/pwa/offline.interceptor';
import { subscriptionErrorInterceptor } from './core/subscription/subscription-error.interceptor';
import { ThemeService } from './core/theme/theme.service';

function initApp(theme: ThemeService, transloco: TranslocoService, auth: AuthService) {
  return () => {
    transloco.setActiveLang(theme.language());
    return auth.restoreSession();
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([authInterceptor, offlineInterceptor, subscriptionErrorInterceptor]),
    ),
    provideTransloco({
      config: {
        availableLangs: ['ar', 'en'],
        defaultLang: 'ar',
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
    {
      provide: APP_INITIALIZER,
      useFactory: initApp,
      deps: [ThemeService, TranslocoService, AuthService],
      multi: true,
    },
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};
