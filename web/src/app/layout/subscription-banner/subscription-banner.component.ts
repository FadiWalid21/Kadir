import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'kadr-subscription-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, ButtonComponent, IconComponent],
  template: `
    @switch (store.bannerKind()) {
      @case ('trialInfo') {
        <div
          class="flex flex-wrap items-center justify-between gap-3 border-b border-line bg-info-soft px-4 py-2 text-sm text-fg"
          role="status"
        >
          <p class="text-start">
            {{ 'subscription.banner.trialInfo' | transloco: { days: store.daysLeft() ?? 0 } }}
          </p>
          <div class="flex flex-wrap items-center gap-2">
            <a routerLink="/settings/subscription" class="font-medium text-accent underline-offset-2 hover:underline">
              {{ 'subscription.banner.viewSubscription' | transloco }}
            </a>
            <kadr-button variant="ghost" type="button" (click)="store.dismissTrialInfoBanner()">
              {{ 'subscription.banner.dismiss' | transloco }}
            </kadr-button>
          </div>
        </div>
      }
      @case ('trialWarn') {
        <div
          class="flex flex-wrap items-center justify-between gap-3 border-b border-line bg-warning-soft px-4 py-2 text-sm text-fg"
          role="status"
        >
          <p class="text-start">
            {{ 'subscription.banner.trialWarn' | transloco: { days: store.daysLeft() ?? 0 } }}
          </p>
          <a routerLink="/settings/subscription" class="font-medium text-accent underline-offset-2 hover:underline">
            {{ 'subscription.banner.viewSubscription' | transloco }}
          </a>
        </div>
      }
      @case ('readOnly') {
        <div
          class="flex flex-wrap items-center justify-between gap-3 border-b border-danger/30 bg-danger-soft px-4 py-2 text-sm text-fg"
          role="alert"
        >
          <div class="flex min-w-0 items-start gap-2 text-start">
            <span class="mt-0.5 text-danger">
              <kadr-icon name="lock" size="sm" />
            </span>
            <p>{{ 'subscription.banner.readOnly' | transloco }}</p>
          </div>
          <a routerLink="/settings/subscription" class="font-medium text-accent underline-offset-2 hover:underline">
            {{ 'subscription.banner.viewSubscription' | transloco }}
          </a>
        </div>
      }
    }
  `,
})
export class SubscriptionBannerComponent {
  protected readonly store = inject(SubscriptionStore);
}
