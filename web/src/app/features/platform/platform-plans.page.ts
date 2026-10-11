import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  WritableSignal,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PlatformApi } from '../../core/api/platform.api';
import { PlatformPlan } from '../../core/api/platform.models';
import { FeatureKeys } from '../../core/subscription/feature-keys';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error' | 'empty';

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
  selector: 'kadr-platform-plans-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DrawerComponent,
    IconComponent,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <header class="flex flex-col gap-2 text-start">
        <h1 class="text-2xl font-bold text-fg">{{ 'platform.plans.title' | transloco }}</h1>
        <p class="text-sm text-fg-2">{{ 'platform.plans.subtitle' | transloco }}</p>
      </header>

      @if (state() === 'loading') {
        <div class="flex flex-col gap-2">
          @for (_ of [1, 2, 3, 4]; track $index) {
            <kadr-skeleton height="3.5rem" />
          }
        </div>
      } @else if (state() === 'error') {
        <kadr-empty-state
          [title]="'platform.plans.loadErrorTitle' | transloco"
          [description]="'platform.plans.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">
            {{ 'home.retry' | transloco }}
          </kadr-button>
        </kadr-empty-state>
      } @else if (state() === 'empty') {
        <kadr-empty-state
          [title]="'platform.plans.emptyTitle' | transloco"
          [description]="'platform.plans.emptyBody' | transloco"
        />
      } @else {
        <div class="overflow-x-auto rounded-lg border border-line bg-surface">
          <table class="w-full text-start text-sm">
            <thead class="border-b border-line bg-subtle text-xs text-fg-2">
              <tr>
                <th class="px-3 py-3 font-medium">{{ 'platform.plans.colName' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.plans.pricing' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.plans.cap' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.plans.unitPrice' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.plans.minBilled' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.plans.subscribers' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.plans.active' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.plans.edit' | transloco }}</th>
              </tr>
            </thead>
            <tbody>
              @for (plan of items(); track plan.id) {
                <tr class="border-b border-line hover:bg-hover">
                  <td class="px-3 py-3">
                    <p class="font-medium text-fg">{{ planName(plan) }}</p>
                    <p class="text-xs text-fg-muted"><bdi>{{ plan.code }}</bdi></p>
                  </td>
                  <td class="px-3 py-3">
                    <kadr-badge tone="neutral" [label]="plan.pricingModel" />
                  </td>
                  <td class="px-3 py-3 tabular-nums text-fg">
                    {{ plan.employeeCap === null ? ('platform.plans.unlimited' | transloco) : plan.employeeCap }}
                  </td>
                  <td class="px-3 py-3 tabular-nums text-fg">
                    {{ plan.unitPrice === null ? '—' : plan.unitPrice + ' ' + plan.currency }}
                  </td>
                  <td class="px-3 py-3 tabular-nums text-fg">
                    {{ plan.minBilledEmployees === null ? '—' : plan.minBilledEmployees }}
                  </td>
                  <td class="px-3 py-3 text-fg-2">
                    <span class="inline-flex items-center gap-1">
                      <kadr-icon name="building" size="sm" />
                      —
                    </span>
                  </td>
                  <td class="px-3 py-3">
                    <label class="inline-flex items-center gap-2 text-sm text-fg">
                      <input
                        type="checkbox"
                        class="size-4 accent-accent"
                        [checked]="plan.isActive"
                        (change)="quickToggleActive(plan, $event)"
                      />
                      {{
                        (plan.isActive ? 'platform.plans.active' : 'platform.plans.inactive')
                          | transloco
                      }}
                    </label>
                  </td>
                  <td class="px-3 py-3">
                    <kadr-button variant="secondary" type="button" (click)="openEdit(plan)">
                      {{ 'platform.plans.edit' | transloco }}
                    </kadr-button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <p class="flex items-start gap-2 text-xs text-fg-muted">
          <kadr-icon name="info" size="sm" />
          {{ 'platform.plans.priceNote' | transloco }}
        </p>
      }
    </div>

    <kadr-drawer
      [open]="!!editing()"
      [title]="'platform.plans.editTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="closeEdit()"
    >
      @if (editing(); as plan) {
        <div class="flex flex-col gap-4">
          <p class="text-sm text-fg-2">
            <bdi>{{ plan.code }}</bdi> · {{ plan.pricingModel }} · {{ plan.currency }}
          </p>
          <p class="rounded-md bg-warning-soft px-3 py-2 text-sm text-warning">
            {{ 'platform.plans.impactNote' | transloco }}
          </p>
          <label class="grid gap-1 text-sm text-fg">
            <span>{{ 'platform.plans.nameAr' | transloco }}</span>
            <input
              class="h-control rounded-md border border-line-strong bg-surface px-3"
              [ngModel]="nameAr()"
              (ngModelChange)="nameAr.set($event)"
            />
          </label>
          <label class="grid gap-1 text-sm text-fg">
            <span>{{ 'platform.plans.nameEn' | transloco }}</span>
            <input
              class="h-control rounded-md border border-line-strong bg-surface px-3"
              [ngModel]="nameEn()"
              (ngModelChange)="nameEn.set($event)"
            />
          </label>
          @if (isFlat(plan)) {
            <label class="grid gap-1 text-sm text-fg">
              <span>{{ 'platform.plans.cap' | transloco }}</span>
              <input
                type="number"
                class="h-control rounded-md border border-line-strong bg-surface px-3"
                [ngModel]="employeeCap()"
                (ngModelChange)="onNullableNumber($event, employeeCap)"
              />
            </label>
          } @else {
            <label class="grid gap-1 text-sm text-fg">
              <span>{{ 'platform.plans.unitPrice' | transloco }}</span>
              <input
                type="number"
                class="h-control rounded-md border border-line-strong bg-surface px-3"
                [ngModel]="unitPrice()"
                (ngModelChange)="onNullableNumber($event, unitPrice)"
              />
            </label>
            <label class="grid gap-1 text-sm text-fg">
              <span>{{ 'platform.plans.minBilled' | transloco }}</span>
              <input
                type="number"
                class="h-control rounded-md border border-line-strong bg-surface px-3"
                [ngModel]="minBilled()"
                (ngModelChange)="onNullableNumber($event, minBilled)"
              />
            </label>
          }
          <label class="inline-flex items-center gap-2 text-sm text-fg">
            <input
              type="checkbox"
              class="size-4 accent-accent"
              [ngModel]="isActive()"
              (ngModelChange)="isActive.set($event)"
            />
            {{ 'platform.plans.active' | transloco }}
          </label>
          <fieldset class="grid gap-2">
            <legend class="text-sm font-medium text-fg">{{ 'platform.plans.features' | transloco }}</legend>
            @for (key of featureKeys; track key) {
              <label class="inline-flex items-center gap-2 text-sm text-fg">
                <input
                  type="checkbox"
                  class="size-4 accent-accent"
                  [checked]="!!features()[key]"
                  (change)="toggleFeature(key, $event)"
                />
                {{ ('subscription.features.' + key) | transloco }}
              </label>
            }
          </fieldset>
          <label class="grid gap-1 text-sm text-fg">
            <span>{{ 'platform.actions.reason' | transloco }}</span>
            <textarea
              class="min-h-20 rounded-md border border-line-strong bg-surface px-3 py-2 text-sm"
              [ngModel]="reason()"
              (ngModelChange)="reason.set($event)"
            ></textarea>
            <span class="text-xs text-fg-muted">{{ 'platform.plans.reasonLocalNote' | transloco }}</span>
          </label>
        </div>
      }
      @if (editing()) {
        <div drawerFooter class="contents">
          <kadr-button variant="primary" type="button" [loading]="busy()" (click)="save()">
            {{ 'platform.plans.saveChanges' | transloco }}
          </kadr-button>
          <kadr-button variant="secondary" type="button" (click)="closeEdit()">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
        </div>
      }
    </kadr-drawer>
  `,
})
export class PlatformPlansPage implements OnInit {
  private readonly api = inject(PlatformApi);
  private readonly i18n = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<PlatformPlan[]>([]);
  protected readonly editing = signal<PlatformPlan | null>(null);
  protected readonly nameAr = signal('');
  protected readonly nameEn = signal('');
  protected readonly employeeCap = signal<number | null>(null);
  protected readonly unitPrice = signal<number | null>(null);
  protected readonly minBilled = signal<number | null>(null);
  protected readonly isActive = signal(true);
  protected readonly features = signal<Record<string, boolean>>({});
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly featureKeys = FEATURE_ORDER;

  ngOnInit(): void {
    void this.reload();
  }

  reload(): void {
    void this.load();
  }

  planName(plan: PlatformPlan): string {
    return this.i18n.getActiveLang() === 'ar' ? plan.nameAr : plan.nameEn;
  }

  isFlat(plan: PlatformPlan): boolean {
    const m = plan.pricingModel.toLowerCase();
    return m === 'flattier' || m === 'flat';
  }

  onNullableNumber(value: string | number | null, target: WritableSignal<number | null>): void {
    if (value === '' || value === null) {
      target.set(null);
      return;
    }
    target.set(+value);
  }

  openEdit(plan: PlatformPlan): void {
    this.editing.set(plan);
    this.nameAr.set(plan.nameAr);
    this.nameEn.set(plan.nameEn);
    this.employeeCap.set(plan.employeeCap);
    this.unitPrice.set(plan.unitPrice);
    this.minBilled.set(plan.minBilledEmployees);
    this.isActive.set(plan.isActive);
    this.reason.set('');
    const map: Record<string, boolean> = {};
    for (const key of FEATURE_ORDER) {
      map[key] = !!plan.features[key];
    }
    this.features.set(map);
  }

  closeEdit(): void {
    this.editing.set(null);
  }

  toggleFeature(key: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.features.update((prev) => ({ ...prev, [key]: checked }));
  }

  async quickToggleActive(plan: PlatformPlan, event: Event): Promise<void> {
    const checked = (event.target as HTMLInputElement).checked;
    this.busy.set(true);
    try {
      const updated = await this.api.updatePlan(plan.id, {
        nameAr: plan.nameAr,
        nameEn: plan.nameEn,
        employeeCap: plan.employeeCap,
        unitPrice: plan.unitPrice,
        minBilledEmployees: plan.minBilledEmployees,
        featuresJson: plan.featuresJson || JSON.stringify(plan.features),
        isActive: checked,
      });
      this.items.update((list) => list.map((p) => (p.id === updated.id ? updated : p)));
      this.toast.show(this.i18n.translate('platform.plans.saveSuccess'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('platform.plans.saveFailed'), 'danger');
      void this.load();
    } finally {
      this.busy.set(false);
    }
  }

  async save(): Promise<void> {
    const plan = this.editing();
    if (!plan) {
      return;
    }
    if (!this.nameAr().trim() || !this.nameEn().trim()) {
      this.toast.show(this.i18n.translate('platform.plans.nameRequired'), 'danger');
      return;
    }
    if (!this.reason().trim()) {
      this.toast.show(this.i18n.translate('platform.actions.reasonRequired'), 'danger');
      return;
    }
    this.busy.set(true);
    try {
      const updated = await this.api.updatePlan(plan.id, {
        nameAr: this.nameAr().trim(),
        nameEn: this.nameEn().trim(),
        employeeCap: this.isFlat(plan) ? this.employeeCap() : null,
        unitPrice: this.isFlat(plan) ? null : this.unitPrice(),
        minBilledEmployees: this.isFlat(plan) ? null : this.minBilled(),
        featuresJson: JSON.stringify(this.features()),
        isActive: this.isActive(),
      });
      this.items.update((list) => list.map((p) => (p.id === updated.id ? updated : p)));
      this.toast.show(this.i18n.translate('platform.plans.saveSuccess'), 'success');
      this.closeEdit();
    } catch {
      this.toast.show(this.i18n.translate('platform.plans.saveFailed'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      const result = await this.api.listPlans();
      this.items.set(result.items.filter((p) => p.code !== 'volume'));
      this.state.set(this.items().length === 0 ? 'empty' : 'ready');
    } catch {
      this.state.set('error');
    }
  }
}
