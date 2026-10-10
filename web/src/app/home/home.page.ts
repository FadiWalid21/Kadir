import { isDevMode } from '@angular/core';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonComponent } from '../shared/ui/button/button.component';
import { CardComponent } from '../shared/ui/card/card.component';

@Component({
  selector: 'kadr-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent, CardComponent],
  template: `
    <main class="mx-auto flex min-h-dvh max-w-3xl flex-col gap-6 p-6">
      <header>
        <p class="text-sm text-fg-2">{{ 'app.name' | transloco }}</p>
        <h1 class="text-2xl font-bold leading-tight text-fg">{{ 'app.homeTitle' | transloco }}</h1>
        <p class="mt-2 text-fg-2">{{ 'app.homeBody' | transloco }}</p>
      </header>
      @if (showUiKit) {
        <kadr-card>
          <kadr-button variant="primary" type="button" (click)="openUiKit()">
            {{ 'app.openUiKit' | transloco }}
          </kadr-button>
        </kadr-card>
      }
    </main>
  `,
})
export class HomePage {
  private readonly router = inject(Router);
  protected readonly showUiKit = isDevMode();

  openUiKit(): void {
    void this.router.navigateByUrl('/dev/ui-kit');
  }
}
