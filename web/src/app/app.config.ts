import { provideHttpClient } from '@angular/common/http';
import {
  ApplicationConfig,
  APP_INITIALIZER,
  isDevMode,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { routes } from './app.routes';
import { TranslocoHttpLoader } from './core/i18n/transloco-loader';
import { ThemeService } from './core/theme/theme.service';

function initThemeAndI18n(theme: ThemeService, transloco: TranslocoService) {
  return () => {
    transloco.setActiveLang(theme.language());
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
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
      useFactory: initThemeAndI18n,
      deps: [ThemeService, TranslocoService],
      multi: true,
    },
  ],
};
