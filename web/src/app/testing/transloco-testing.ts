import { provideTransloco, TranslocoLoader } from '@jsverse/transloco';
import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import ar from '../../assets/i18n/ar.json';
import en from '../../assets/i18n/en.json';

@Injectable({ providedIn: 'root' })
class InlineTranslocoLoader implements TranslocoLoader {
  getTranslation(lang: string) {
    return of(lang === 'en' ? en : ar);
  }
}

export function provideTestTransloco() {
  return provideTransloco({
    config: {
      availableLangs: ['ar', 'en'],
      defaultLang: 'ar',
      reRenderOnLangChange: true,
      prodMode: true,
    },
    loader: InlineTranslocoLoader,
  });
}
