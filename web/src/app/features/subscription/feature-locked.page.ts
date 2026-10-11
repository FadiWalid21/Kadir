import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { map } from 'rxjs';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'kadr-feature-locked-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, ButtonComponent, IconComponent],
  template: `
    <div class="mx-auto flex w-full max-w-md flex-col items-center gap-4 px-4 py-16 text-center">
      <span class="text-fg-2">
        <kadr-icon name="lock" size="lg" />
      </span>
      <h1 class="text-xl font-bold text-fg">{{ 'subscription.featureLocked.title' | transloco }}</h1>
      <p class="text-sm text-fg-2">
        {{ 'subscription.featureLocked.body' | transloco: { plan: planHint() } }}
      </p>
      <div class="mt-2 flex flex-wrap justify-center gap-2">
        <a routerLink="/settings/subscription">
          <kadr-button variant="primary" type="button">
            {{ 'subscription.featureLocked.viewPlans' | transloco }}
          </kadr-button>
        </a>
        <kadr-button variant="secondary" type="button" (click)="goBack()">
          {{ 'subscription.featureLocked.back' | transloco }}
        </kadr-button>
      </div>
    </div>
  `,
})
export class FeatureLockedPage {
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);

  private readonly feature = toSignal(
    this.route.queryParamMap.pipe(map((p) => p.get('feature') ?? '')),
    { initialValue: '' },
  );

  protected readonly planHint = computed(() => {
    const key = this.feature();
    const mapKey = `subscription.featurePlans.${key}`;
    const translated = this.transloco.translate(mapKey);
    return translated === mapKey
      ? this.transloco.translate('subscription.featurePlans.default')
      : translated;
  });

  goBack(): void {
    history.back();
  }
}
