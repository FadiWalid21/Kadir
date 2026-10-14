import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonComponent } from '../../shared/ui/button/button.component';

@Component({
  selector: 'kadr-forbidden-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, ButtonComponent],
  template: `
    <main class="flex min-h-dvh flex-col items-center justify-center gap-4 bg-app px-6 text-center">
      <h1 class="text-xl font-bold text-fg">{{ 'auth.forbiddenTitle' | transloco }}</h1>
      <p class="max-w-sm text-sm text-fg-2">{{ 'auth.forbiddenBody' | transloco }}</p>
      <a routerLink="/home">
        <kadr-button variant="secondary" type="button">{{ 'auth.backHome' | transloco }}</kadr-button>
      </a>
    </main>
  `,
})
export class ForbiddenPage {}
