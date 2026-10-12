import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  PayComponent,
  PayComponentsApi,
} from '../../core/api/pay-components.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';
type DrawerMode = 'create' | 'edit';
type KindTab = 'all' | 'Earning' | 'Deduction' | 'EmployerCost';

@Component({
  selector: 'kadr-pay-components-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    SkeletonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    DrawerComponent,
    ConfirmDialogComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-5">
      <header class="flex flex-col gap-3 text-start sm:flex-row sm:items-end sm:justify-between">
        <div class="flex min-w-0 flex-col gap-2">
          <div class="flex flex-wrap items-center gap-2">
            <h1 class="text-2xl font-bold leading-tight text-fg">
              {{ 'settings.payComponents.title' | transloco }}
            </h1>
            @if (state() === 'ready') {
              <span
                class="rounded-full bg-accent-soft px-2.5 py-0.5 text-xs font-medium text-accent-soft-fg tabular-nums"
              >
                {{ 'settings.payComponents.activeCount' | transloco: { n: activeCount() } }}
              </span>
            }
          </div>
          <p class="text-sm text-fg-2">{{ 'settings.payComponents.subtitle' | transloco }}</p>
        </div>
        @if (canMutate()) {
          <kadr-button variant="primary" type="button" (click)="openCreate()">
            {{ 'settings.payComponents.add' | transloco }}
          </kadr-button>
        }
      </header>

      @if (state() === 'loading') {
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
        </div>
        <kadr-skeleton height="16rem" />
      } @else if (state() === 'error') {
        <kadr-network-error
          [title]="'settings.payComponents.loadErrorTitle' | transloco"
          [description]="'settings.payComponents.loadErrorBody' | transloco"
          (retry)="reload()"
        />
      } @else {
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'settings.payComponents.tileTotal' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ counts().total }}</p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'settings.payComponents.tileEarning' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ counts().earning }}</p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'settings.payComponents.tileDeduction' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ counts().deduction }}</p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'settings.payComponents.tileEmployer' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ counts().employer }}</p>
          </div>
        </div>

        <div class="flex flex-col gap-3">
          <div
            class="flex flex-wrap gap-1 rounded-lg bg-subtle p-1"
            role="tablist"
            [attr.aria-label]="'settings.payComponents.kindTabsAria' | transloco"
          >
            @for (tab of kindTabs; track tab.id) {
              <button
                type="button"
                role="tab"
                class="rounded-md px-3 py-1.5 text-sm transition-colors"
                [class.bg-surface]="kindTab() === tab.id"
                [class.text-fg]="kindTab() === tab.id"
                [class.font-medium]="kindTab() === tab.id"
                [class.shadow-sm]="kindTab() === tab.id"
                [class.text-fg-2]="kindTab() !== tab.id"
                [attr.aria-selected]="kindTab() === tab.id"
                (click)="kindTab.set(tab.id)"
              >
                {{ tab.labelKey | transloco }}
                <span class="ms-1 tabular-nums text-fg-muted">({{ kindCount(tab.id) }})</span>
              </button>
            }
          </div>

          <div class="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div class="min-w-0 flex-1">
              <kadr-input
                [label]="'settings.payComponents.search' | transloco"
                [value]="query()"
                (valueChange)="onSearch($event)"
              />
            </div>
            <label class="flex flex-col gap-2 text-sm text-fg">
              <span class="font-medium">{{ 'settings.payComponents.statusFilter' | transloco }}</span>
              <select
                class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
                [value]="statusFilter()"
                (change)="onStatusFilter($any($event.target).value)"
              >
                <option value="active">{{ 'ui.statusActive' | transloco }}</option>
                <option value="inactive">{{ 'settings.payComponents.inactive' | transloco }}</option>
                <option value="all">{{ 'settings.payComponents.statusAll' | transloco }}</option>
              </select>
            </label>
          </div>
        </div>

        @if (filteredItems().length === 0) {
          <kadr-empty-state
            [title]="'settings.payComponents.emptyTitle' | transloco"
            [description]="'settings.payComponents.emptyBody' | transloco"
          >
            @if (canMutate()) {
              <kadr-button variant="primary" type="button" (click)="openCreate()">
                {{ 'settings.payComponents.add' | transloco }}
              </kadr-button>
            }
          </kadr-empty-state>
        } @else {
          <!-- Desktop table -->
          <div class="hidden overflow-x-auto rounded-xl border border-line bg-surface shadow-sm md:block">
            <table class="w-full text-start text-sm">
              <thead class="bg-subtle text-xs text-fg-2">
                <tr>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.payComponents.colName' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.payComponents.kind' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.payComponents.isTaxable' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.payComponents.isInsurable' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.payComponents.calcType' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.payComponents.colStatus' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.payComponents.colActions' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of filteredItems(); track item.id) {
                  <tr class="border-t border-line hover:bg-subtle/60">
                    <td class="px-3 py-3">
                      <button
                        type="button"
                        class="text-start"
                        (click)="canMutate() && openEdit(item)"
                      >
                        <div class="font-medium text-fg">{{ displayName(item) }}</div>
                        <div class="mt-0.5 font-mono text-xs text-fg-muted">{{ item.key }}</div>
                      </button>
                    </td>
                    <td class="px-3 py-3">
                      <span [class]="kindBadgeClass(item.kind)">{{ kindLabel(item.kind) }}</span>
                    </td>
                    <td class="px-3 py-3">
                      <span class="rounded bg-subtle px-2 py-0.5 text-xs text-fg-2">
                        {{
                          (item.isTaxable
                            ? 'settings.payComponents.taxableYes'
                            : 'settings.payComponents.taxableNo'
                          ) | transloco
                        }}
                      </span>
                    </td>
                    <td class="px-3 py-3">
                      <span class="rounded bg-subtle px-2 py-0.5 text-xs text-fg-2">
                        {{
                          (item.isInsurable
                            ? 'settings.payComponents.insurableYes'
                            : 'settings.payComponents.insurableNo'
                          ) | transloco
                        }}
                      </span>
                    </td>
                    <td class="px-3 py-3 text-fg-2">{{ calcLabel(item.calcType) }}</td>
                    <td class="px-3 py-3">
                      <div class="flex flex-col gap-1">
                        <span
                          class="inline-flex w-fit items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
                          [class.bg-success-soft]="item.isActive"
                          [class.text-success]="item.isActive"
                          [class.bg-subtle]="!item.isActive"
                          [class.text-fg-2]="!item.isActive"
                        >
                          {{
                            (item.isActive ? 'ui.statusActive' : 'settings.payComponents.inactive')
                              | transloco
                          }}
                        </span>
                        @if (item.sourceKey) {
                          <span class="text-xs text-fg-muted">
                            {{ 'settings.payComponents.fromTemplate' | transloco }}
                          </span>
                        }
                      </div>
                    </td>
                    <td class="px-3 py-3">
                      @if (canMutate()) {
                        <div class="flex flex-wrap gap-2">
                          <kadr-button variant="secondary" type="button" size="sm" (click)="openEdit(item)">
                            {{ 'settings.payComponents.edit' | transloco }}
                          </kadr-button>
                          @if (item.isActive) {
                            <kadr-button
                              variant="danger-soft"
                              type="button"
                              size="sm"
                              (click)="askDeactivate(item)"
                            >
                              {{ 'settings.payComponents.deactivate' | transloco }}
                            </kadr-button>
                          }
                        </div>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Mobile cards -->
          <ul class="flex flex-col gap-2 md:hidden">
            @for (item of filteredItems(); track item.id) {
              <li class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
                <div class="flex items-start justify-between gap-2">
                  <div class="min-w-0">
                    <p class="font-medium text-fg">{{ displayName(item) }}</p>
                    <p class="mt-0.5 font-mono text-xs text-fg-muted">{{ item.key }}</p>
                  </div>
                  <span [class]="kindBadgeClass(item.kind)">{{ kindLabel(item.kind) }}</span>
                </div>
                <dl class="mt-3 grid grid-cols-2 gap-2 text-xs">
                  <div>
                    <dt class="text-fg-2">{{ 'settings.payComponents.isTaxable' | transloco }}</dt>
                    <dd class="mt-0.5 text-fg">
                      {{
                        (item.isTaxable
                          ? 'settings.payComponents.taxableYes'
                          : 'settings.payComponents.taxableNo'
                        ) | transloco
                      }}
                    </dd>
                  </div>
                  <div>
                    <dt class="text-fg-2">{{ 'settings.payComponents.isInsurable' | transloco }}</dt>
                    <dd class="mt-0.5 text-fg">
                      {{
                        (item.isInsurable
                          ? 'settings.payComponents.insurableYes'
                          : 'settings.payComponents.insurableNo'
                        ) | transloco
                      }}
                    </dd>
                  </div>
                  <div>
                    <dt class="text-fg-2">{{ 'settings.payComponents.calcType' | transloco }}</dt>
                    <dd class="mt-0.5 text-fg">{{ calcLabel(item.calcType) }}</dd>
                  </div>
                  <div>
                    <dt class="text-fg-2">{{ 'settings.payComponents.colStatus' | transloco }}</dt>
                    <dd class="mt-0.5 text-fg">
                      {{
                        (item.isActive ? 'ui.statusActive' : 'settings.payComponents.inactive')
                          | transloco
                      }}
                    </dd>
                  </div>
                </dl>
                @if (canMutate()) {
                  <div class="mt-3 flex flex-wrap gap-2">
                    <kadr-button variant="secondary" type="button" size="sm" (click)="openEdit(item)">
                      {{ 'settings.payComponents.edit' | transloco }}
                    </kadr-button>
                    @if (item.isActive) {
                      <kadr-button
                        variant="danger-soft"
                        type="button"
                        size="sm"
                        (click)="askDeactivate(item)"
                      >
                        {{ 'settings.payComponents.deactivate' | transloco }}
                      </kadr-button>
                    }
                  </div>
                }
              </li>
            }
          </ul>

          <p class="text-xs text-fg-muted tabular-nums">
            {{
              'settings.payComponents.showingCount'
                | transloco: { n: filteredItems().length, total: items().length }
            }}
          </p>
        }
      }
    </div>

    <kadr-drawer
      [open]="drawerOpen()"
      [title]="
        (mode() === 'create'
          ? 'settings.payComponents.addTitle'
          : 'settings.payComponents.editTitle'
        ) | transloco
      "
      [closeLabel]="'ui.cancel' | transloco"
      [hasFooter]="false"
      (closed)="closeDrawer()"
    >
      <form class="flex flex-col gap-4" (ngSubmit)="onSave()">
        @if (mode() === 'create') {
          <kadr-input
            [label]="'settings.payComponents.key' | transloco"
            [value]="editKey()"
            (valueChange)="editKey.set($event)"
          />
          <p class="text-xs text-fg-muted">{{ 'settings.payComponents.keyHint' | transloco }}</p>
        } @else {
          <p class="text-sm text-fg-2">
            <span class="font-medium">{{ 'settings.payComponents.key' | transloco }}:</span>
            <span class="ms-1 font-mono">{{ editKey() }}</span>
          </p>
        }
        <kadr-input
          [label]="'settings.payComponents.nameAr' | transloco"
          [value]="editNameAr()"
          (valueChange)="editNameAr.set($event)"
        />
        <kadr-input
          [label]="'settings.payComponents.nameEn' | transloco"
          [value]="editNameEn()"
          (valueChange)="editNameEn.set($event)"
        />
        <label class="flex flex-col gap-2 text-sm text-fg">
          <span class="font-medium">{{ 'settings.payComponents.kind' | transloco }}</span>
          <select
            class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
            [ngModel]="editKind()"
            (ngModelChange)="editKind.set($event)"
            name="kind"
          >
            <option value="Earning">{{ 'settings.payComponents.kinds.Earning' | transloco }}</option>
            <option value="Deduction">{{ 'settings.payComponents.kinds.Deduction' | transloco }}</option>
            <option value="EmployerCost">{{ 'settings.payComponents.kinds.EmployerCost' | transloco }}</option>
          </select>
        </label>
        <label class="flex flex-col gap-2 text-sm text-fg">
          <span class="font-medium">{{ 'settings.payComponents.calcType' | transloco }}</span>
          <select
            class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
            [ngModel]="editCalcType()"
            (ngModelChange)="editCalcType.set($event)"
            name="calcType"
          >
            <option value="Fixed">{{ 'settings.payComponents.calcTypes.Fixed' | transloco }}</option>
            <option value="PercentOfBasic">{{ 'settings.payComponents.calcTypes.PercentOfBasic' | transloco }}</option>
            <option value="PercentOfGross">{{ 'settings.payComponents.calcTypes.PercentOfGross' | transloco }}</option>
          </select>
        </label>
        <label class="flex items-center gap-2 text-sm text-fg">
          <input
            type="checkbox"
            class="size-4 rounded border-line"
            [ngModel]="editTaxable()"
            (ngModelChange)="editTaxable.set($event)"
            name="taxable"
          />
          {{ 'settings.payComponents.isTaxable' | transloco }}
        </label>
        <label class="flex items-center gap-2 text-sm text-fg">
          <input
            type="checkbox"
            class="size-4 rounded border-line"
            [ngModel]="editInsurable()"
            (ngModelChange)="editInsurable.set($event)"
            name="insurable"
          />
          {{ 'settings.payComponents.isInsurable' | transloco }}
        </label>
        <div class="mt-2 flex flex-wrap justify-end gap-2">
          <kadr-button variant="secondary" type="button" (click)="closeDrawer()">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
          <kadr-button variant="primary" type="submit" [disabled]="!canMutate()" [loading]="busy()">
            {{ 'ui.save' | transloco }}
          </kadr-button>
        </div>
      </form>
    </kadr-drawer>

    <kadr-confirm-dialog
      [open]="!!pendingDeactivate()"
      [title]="'settings.payComponents.deactivateTitle' | transloco"
      [message]="'settings.payComponents.deactivateBody' | transloco"
      [confirmLabel]="'settings.payComponents.deactivate' | transloco"
      [cancelLabel]="'ui.cancel' | transloco"
      [danger]="true"
      [busy]="busy()"
      (confirmed)="confirmDeactivate()"
      (cancelled)="pendingDeactivate.set(null)"
    />
  `,
})
export class PayComponentsSettingsPage implements OnInit {
  private readonly api = inject(PayComponentsApi);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly title = inject(Title);

  protected readonly kindTabs: { id: KindTab; labelKey: string }[] = [
    { id: 'all', labelKey: 'settings.payComponents.tabAll' },
    { id: 'Earning', labelKey: 'settings.payComponents.kinds.Earning' },
    { id: 'Deduction', labelKey: 'settings.payComponents.kinds.Deduction' },
    { id: 'EmployerCost', labelKey: 'settings.payComponents.kinds.EmployerCost' },
  ];

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<PayComponent[]>([]);
  protected readonly query = signal('');
  protected readonly statusFilter = signal<'active' | 'inactive' | 'all'>('active');
  protected readonly kindTab = signal<KindTab>('all');
  protected readonly busy = signal(false);
  protected readonly drawerOpen = signal(false);
  protected readonly mode = signal<DrawerMode>('create');
  protected readonly editingId = signal<string | null>(null);
  protected readonly editKey = signal('');
  protected readonly editNameAr = signal('');
  protected readonly editNameEn = signal('');
  protected readonly editKind = signal('Earning');
  protected readonly editCalcType = signal('Fixed');
  protected readonly editTaxable = signal(true);
  protected readonly editInsurable = signal(false);
  protected readonly pendingDeactivate = signal<PayComponent | null>(null);

  protected readonly canMutate = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.SalaryManage) && !this.subscription.readOnly(),
  );

  protected readonly counts = computed(() => {
    const list = this.items();
    return {
      total: list.length,
      earning: list.filter((x) => x.kind === 'Earning').length,
      deduction: list.filter((x) => x.kind === 'Deduction').length,
      employer: list.filter((x) => x.kind === 'EmployerCost').length,
    };
  });

  protected readonly activeCount = computed(
    () => this.items().filter((x) => x.isActive).length,
  );

  protected readonly filteredItems = computed(() => {
    const tab = this.kindTab();
    const list = this.items();
    if (tab === 'all') return list;
    return list.filter((x) => x.kind === tab);
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('settings.payComponents.title'));
    void this.reload();
  }

  kindCount(tab: KindTab): number {
    if (tab === 'all') return this.counts().total;
    if (tab === 'Earning') return this.counts().earning;
    if (tab === 'Deduction') return this.counts().deduction;
    return this.counts().employer;
  }

  displayName(item: PayComponent): string {
    return this.transloco.getActiveLang() === 'en' ? item.nameEn : item.nameAr;
  }

  kindLabel(kind: string): string {
    return this.transloco.translate(`settings.payComponents.kinds.${kind}`);
  }

  calcLabel(calc: string): string {
    return this.transloco.translate(`settings.payComponents.calcTypes.${calc}`);
  }

  kindBadgeClass(kind: string): string {
    const base = 'inline-flex rounded px-2 py-0.5 text-xs font-medium ';
    if (kind === 'Earning') return base + 'bg-success-soft text-success';
    if (kind === 'Deduction') return base + 'bg-danger-soft text-danger';
    return base + 'bg-subtle text-fg-2';
  }

  async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const isActive =
        this.statusFilter() === 'all' ? null : this.statusFilter() === 'active';
      const page = await this.api.list({
        page: 1,
        pageSize: 100,
        q: this.query() || undefined,
        isActive,
      });
      this.items.set(page.items);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  onSearch(value: string): void {
    this.query.set(value);
    void this.reload();
  }

  onStatusFilter(value: string): void {
    if (value === 'active' || value === 'inactive' || value === 'all') {
      this.statusFilter.set(value);
      void this.reload();
    }
  }

  openCreate(): void {
    this.mode.set('create');
    this.editingId.set(null);
    this.editKey.set('');
    this.editNameAr.set('');
    this.editNameEn.set('');
    this.editKind.set('Earning');
    this.editCalcType.set('Fixed');
    this.editTaxable.set(true);
    this.editInsurable.set(false);
    this.drawerOpen.set(true);
  }

  openEdit(item: PayComponent): void {
    this.mode.set('edit');
    this.editingId.set(item.id);
    this.editKey.set(item.key);
    this.editNameAr.set(item.nameAr);
    this.editNameEn.set(item.nameEn);
    this.editKind.set(item.kind);
    this.editCalcType.set(item.calcType);
    this.editTaxable.set(item.isTaxable);
    this.editInsurable.set(item.isInsurable);
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  askDeactivate(item: PayComponent): void {
    this.pendingDeactivate.set(item);
  }

  async confirmDeactivate(): Promise<void> {
    const item = this.pendingDeactivate();
    if (!item) {
      return;
    }
    this.busy.set(true);
    try {
      const updated = await this.api.deactivate(item.id);
      this.pendingDeactivate.set(null);
      if (this.statusFilter() === 'active') {
        this.items.update((list) => list.filter((x) => x.id !== updated.id));
      } else {
        this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      }
      this.toast.show(this.transloco.translate('settings.payComponents.toastDeactivated'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('settings.payComponents.toastFail'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  async onSave(): Promise<void> {
    const nameAr = this.editNameAr().trim();
    const nameEn = this.editNameEn().trim();
    if (!nameAr || !nameEn) {
      this.toast.show(this.transloco.translate('settings.payComponents.toastValidation'), 'danger');
      return;
    }
    if (this.mode() === 'create') {
      const key = this.editKey().trim().toLowerCase();
      if (!/^[a-z0-9_]+$/.test(key)) {
        this.toast.show(this.transloco.translate('settings.payComponents.toastKeyInvalid'), 'danger');
        return;
      }
    }

    this.busy.set(true);
    try {
      if (this.mode() === 'create') {
        const key = this.editKey().trim().toLowerCase();
        const created = await this.api.create({
          key,
          nameAr,
          nameEn,
          kind: this.editKind(),
          calcType: this.editCalcType(),
          isTaxable: this.editTaxable(),
          isInsurable: this.editInsurable(),
        });
        this.items.update((list) => [...list, created].sort((a, b) => a.key.localeCompare(b.key)));
      } else {
        const id = this.editingId();
        if (!id) {
          return;
        }
        const updated = await this.api.update(id, {
          nameAr,
          nameEn,
          kind: this.editKind(),
          calcType: this.editCalcType(),
          isTaxable: this.editTaxable(),
          isInsurable: this.editInsurable(),
        });
        this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      }
      this.closeDrawer();
      this.toast.show(this.transloco.translate('settings.payComponents.toastSaved'), 'success');
    } catch (err) {
      const code = problemDetailsFromError(err)?.code;
      const key =
        code === 'salary.duplicate_key'
          ? 'settings.payComponents.toastDuplicate'
          : 'settings.payComponents.toastFail';
      this.toast.show(this.transloco.translate(key), 'danger');
    } finally {
      this.busy.set(false);
    }
  }
}
