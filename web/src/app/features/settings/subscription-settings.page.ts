import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SubscriptionApi } from '../../core/api/subscription.api';
import {
  PlanCatalogItemDto,
  SubscriptionDto,
} from '../../core/api/subscription.models';
import { FeatureKeys } from '../../core/subscription/feature-keys';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ThemeService } from '../../core/theme/theme.service';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';

type PageState = 'loading' | 'ready' | 'error';
type PricingView = 'flat' | 'perEmployee';

const FEATURE_ORDER = [
  FeatureKeys.CoreHr,
  FeatureKeys.Attendance,
  FeatureKeys.Leave,
  FeatureKeys.Ess,
  FeatureKeys.Shifts,
  FeatureKeys.Workflows,
  FeatureKeys.Reports,
  FeatureKeys.Documents,
  FeatureKeys.DeviceImport,
  FeatureKeys.CustomFields,
  FeatureKeys.Api,
  FeatureKeys.ComplianceDashboards,
  FeatureKeys.PrioritySupport,
] as const;

@Component({
  selector: 'kadr-subscription-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    ButtonComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
  ],
  template: `
    @if (state() === 'loading') {
      <div class="flex flex-col gap-4" role="status" [attr.aria-label]="'ui.loading' | transloco">
        <kadr-skeleton height="2rem" width="40%" />
        <kadr-skeleton height="10rem" />
        <kadr-skeleton height="16rem" />
      </div>
    } @else if (state() === 'error') {
      <kadr-empty-state
        [title]="'settings.subscription.loadErrorTitle' | transloco"
        [description]="'settings.subscription.loadErrorBody' | transloco"
      >
        <kadr-button variant="secondary" type="button" (click)="reload()">
          {{ 'home.retry' | transloco }}
        </kadr-button>
      </kadr-empty-state>
    } @else if (sub()) {
      <div class="mx-auto flex w-full max-w-4xl flex-col gap-6">
        <header class="flex flex-col gap-2 text-start">
          <h1 class="text-2xl font-bold leading-tight text-fg">
            {{ 'settings.subscription.title' | transloco }}
          </h1>
          <p class="text-sm text-fg-2">{{ 'settings.subscription.subtitle' | transloco }}</p>
        </header>

        <!-- Current plan -->
        <section class="rounded-lg border border-line bg-surface p-5 shadow-sm sm:p-6">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="text-start">
              <h2 class="text-lg font-bold text-fg">{{ planDisplayName() }}</h2>
              <p class="mt-1 text-sm text-fg-2">
                @if (sub()!.trialEndsAt) {
                  {{ 'settings.subscription.endsOn' | transloco: { date: endDateLabel() } }}
                } @else {
                  {{ 'settings.subscription.statusActive' | transloco }}
                }
              </p>
            </div>
            <kadr-badge [tone]="statusTone()" [label]="statusLabel()" />
          </div>

          @if (sub()!.daysLeft !== null && sub()!.effectiveState === 'trialRunning') {
            <div class="mt-4">
              <div class="mb-1 flex justify-between text-xs text-fg-2">
                <span>{{ 'settings.subscription.daysLeft' | transloco: { days: sub()!.daysLeft } }}</span>
                <span>14</span>
              </div>
              <div class="h-2 overflow-hidden rounded-full bg-subtle" role="progressbar"
                [attr.aria-valuenow]="trialProgress()"
                aria-valuemin="0"
                aria-valuemax="100">
                <div class="h-full rounded-full bg-accent" [style.width.%]="trialProgress()"></div>
              </div>
            </div>
          }

          <div class="mt-4 flex justify-end">
            <a [href]="contactHref()" class="inline-flex">
              <kadr-button variant="secondary" type="button">
                {{ 'settings.subscription.contactUs' | transloco }}
              </kadr-button>
            </a>
          </div>
        </section>

        <!-- Usage -->
        <section class="rounded-lg border border-line bg-surface p-5 shadow-sm sm:p-6">
          <h2 class="text-start text-lg font-bold text-fg">
            {{ 'settings.subscription.usageTitle' | transloco }}
          </h2>
          <div class="mt-3">
            <div class="mb-1 flex justify-between text-sm text-fg-2">
              <span>{{ 'settings.subscription.activeEmployees' | transloco }}</span>
              <span>
                {{ sub()!.usage.activeEmployees }}
                @if (sub()!.usage.cap !== null) {
                  / {{ sub()!.usage.cap }}
                }
              </span>
            </div>
            <div class="h-2 overflow-hidden rounded-full bg-subtle">
              <div
                class="h-full rounded-full bg-accent"
                [style.width.%]="usagePercent()"
              ></div>
            </div>
          </div>
        </section>

        <!-- Includes -->
        <section class="rounded-lg border border-line bg-surface p-5 shadow-sm sm:p-6">
          <h2 class="text-start text-lg font-bold text-fg">
            {{ 'settings.subscription.includesTitle' | transloco }}
          </h2>
          <ul class="mt-3 flex flex-col gap-2 text-start text-sm">
            @for (key of enabledFeatureKeys(); track key) {
              <li class="text-fg">{{ featureLabel(key) }}</li>
            }
          </ul>
        </section>

        <!-- Comparison -->
        <section class="rounded-lg border border-line bg-surface p-5 shadow-sm sm:p-6">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <h2 class="text-start text-lg font-bold text-fg">
              {{ 'settings.subscription.compareTitle' | transloco }}
            </h2>
            <div class="flex rounded-md border border-line p-0.5" role="group"
              [attr.aria-label]="'settings.subscription.pricingToggleAria' | transloco">
              <button
                type="button"
                class="rounded-sm px-3 py-1.5 text-sm"
                [class.bg-accent-soft]="pricingView() === 'flat'"
                [class.text-fg]="pricingView() === 'flat'"
                [class.text-fg-2]="pricingView() !== 'flat'"
                (click)="pricingView.set('flat')"
              >
                {{ 'settings.subscription.flatTier' | transloco }}
              </button>
              <button
                type="button"
                class="rounded-sm px-3 py-1.5 text-sm"
                [class.bg-accent-soft]="pricingView() === 'perEmployee'"
                [class.text-fg]="pricingView() === 'perEmployee'"
                [class.text-fg-2]="pricingView() !== 'perEmployee'"
                (click)="pricingView.set('perEmployee')"
              >
                {{ 'settings.subscription.perEmployee' | transloco }}
              </button>
            </div>
          </div>

          @if (pricingView() === 'perEmployee') {
            <p class="mt-3 text-start text-sm text-fg-2">
              {{ 'settings.subscription.perEmployeeExplain' | transloco }}
            </p>
            <p class="mt-1 text-start text-sm text-fg">
              {{
                'settings.subscription.perEmployeeExample'
                  | transloco
                    : {
                        unit: coreUnitPrice(),
                        currency: currency(),
                        min: coreMinBilled(),
                        total: coreExampleTotal(),
                      }
              }}
            </p>
          }

          <div class="mt-4 overflow-x-auto">
            <table class="w-full min-w-0 border-collapse text-start text-sm">
              <thead>
                <tr class="border-b border-line text-fg-2">
                  <th class="px-2 py-2 font-medium">{{ 'settings.subscription.colFeature' | transloco }}</th>
                  @for (plan of visiblePlans(); track plan.code) {
                    <th class="px-2 py-2 font-medium">{{ planName(plan) }}</th>
                  }
                  <th class="px-2 py-2 font-medium text-fg-muted">
                    {{ 'settings.subscription.enterprise' | transloco }}
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr class="border-b border-line">
                  <td class="px-2 py-2 text-fg-2">{{ 'settings.subscription.colPrice' | transloco }}</td>
                  @for (plan of visiblePlans(); track plan.code) {
                    <td class="px-2 py-2 text-fg">{{ priceLabel(plan) }}</td>
                  }
                  <td class="px-2 py-2 text-fg-muted">
                    {{ 'settings.subscription.customPrice' | transloco }}
                  </td>
                </tr>
                @for (key of FEATURE_ORDER; track key) {
                  <tr class="border-b border-line last:border-b-0">
                    <td class="px-2 py-2 text-fg">{{ featureLabel(key) }}</td>
                    @for (plan of visiblePlans(); track plan.code) {
                      <td class="px-2 py-2 text-fg-2">
                        {{ plan.features[key] ? ('settings.subscription.yes' | transloco) : '—' }}
                      </td>
                    }
                    <td class="px-2 py-2 text-fg-muted">{{ 'settings.subscription.yes' | transloco }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          @if (pricingView() === 'perEmployee') {
            <ul class="mt-4 flex flex-col gap-1 text-start text-sm text-fg-2">
              <li>{{ 'settings.subscription.tierCore' | transloco }}</li>
              <li class="text-fg-muted">{{ 'settings.subscription.tierCorePayroll' | transloco }}</li>
              <li class="text-fg-muted">{{ 'settings.subscription.tierComplete' | transloco }}</li>
            </ul>
          }

          <p class="mt-4 text-start text-xs text-fg-2">
            {{ 'settings.subscription.manualChangeNote' | transloco }}
          </p>
        </section>
      </div>
    }
  `,
})
export class SubscriptionSettingsPage implements OnInit {
  private readonly api = inject(SubscriptionApi);
  private readonly store = inject(SubscriptionStore);
  private readonly transloco = inject(TranslocoService);
  private readonly theme = inject(ThemeService);

  protected readonly FEATURE_ORDER = FEATURE_ORDER;
  protected readonly state = signal<PageState>('loading');
  protected readonly sub = signal<SubscriptionDto | null>(null);
  protected readonly plans = signal<PlanCatalogItemDto[]>([]);
  protected readonly pricingView = signal<PricingView>('flat');

  protected readonly planDisplayName = computed(() => {
    const s = this.sub();
    if (!s) {
      return '';
    }
    return this.theme.language() === 'en' ? s.plan.nameEn : s.plan.nameAr;
  });

  protected readonly enabledFeatureKeys = computed(() =>
    FEATURE_ORDER.filter((k) => !!this.sub()?.features[k]),
  );

  protected readonly visiblePlans = computed(() => {
    const all = this.plans();
    if (this.pricingView() === 'flat') {
      return all.filter((p) => p.pricingModel === 'flatTier');
    }
    return all.filter((p) => p.pricingModel === 'perEmployee');
  });

  protected readonly coreUnitPrice = computed(() => {
    const core = this.plans().find((p) => p.code === 'core');
    return core?.unitPrice ?? 0;
  });

  protected readonly coreMinBilled = computed(() => {
    const core = this.plans().find((p) => p.code === 'core');
    return core?.minBilledEmployees ?? 10;
  });

  protected readonly coreExampleTotal = computed(
    () => this.coreUnitPrice() * this.coreMinBilled(),
  );

  protected readonly currency = computed(() => this.sub()?.plan.currency ?? 'EGP');

  ngOnInit(): void {
    void this.reload();
  }

  reload(): void {
    void this.load();
  }

  protected contactHref(): string {
    return 'mailto:support@kadr.app';
  }

  protected featureLabel(key: string): string {
    return this.transloco.translate(`subscription.features.${key}`);
  }

  protected planName(plan: PlanCatalogItemDto): string {
    return this.theme.language() === 'en' ? plan.nameEn : plan.nameAr;
  }

  protected priceLabel(plan: PlanCatalogItemDto): string {
    if (plan.unitPrice == null) {
      return '—';
    }
    if (plan.pricingModel === 'perEmployee') {
      return this.transloco.translate('settings.subscription.pricePerSeat', {
        price: plan.unitPrice,
        currency: plan.currency,
      });
    }
    return this.transloco.translate('settings.subscription.priceMonthly', {
      price: plan.unitPrice,
      currency: plan.currency,
    });
  }

  protected statusTone(): 'info' | 'warning' | 'danger' | 'success' | 'neutral' {
    const state = this.sub()?.effectiveState;
    switch (state) {
      case 'trialRunning':
        return 'info';
      case 'active':
        return 'success';
      case 'trialExpired':
      case 'suspended':
      case 'cancelled':
      case 'pastDue':
        return 'danger';
      default:
        return 'neutral';
    }
  }

  protected statusLabel(): string {
    const state = this.sub()?.effectiveState ?? 'active';
    return this.transloco.translate(`settings.subscription.states.${state}`);
  }

  protected endDateLabel(): string {
    const raw = this.sub()?.trialEndsAt;
    if (!raw) {
      return '';
    }
    return new Date(raw).toLocaleDateString(this.theme.language() === 'en' ? 'en' : 'ar');
  }

  protected trialProgress(): number {
    const days = this.sub()?.daysLeft;
    if (days == null) {
      return 0;
    }
    return Math.max(0, Math.min(100, Math.round((days / 14) * 100)));
  }

  protected usagePercent(): number {
    const usage = this.sub()?.usage;
    if (!usage?.cap || usage.cap <= 0) {
      return 0;
    }
    return Math.max(0, Math.min(100, Math.round((usage.activeEmployees / usage.cap) * 100)));
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      const [sub, plans] = await Promise.all([
        this.api.getSubscription(),
        this.api.listPlans(),
      ]);
      this.sub.set(sub);
      this.plans.set(plans);
      this.store.applyFromMe({
        state: sub.effectiveState,
        trialEndsAt: sub.trialEndsAt,
        daysLeft: sub.daysLeft,
        readOnly: sub.readOnly,
        features: sub.features,
      });
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }
}
