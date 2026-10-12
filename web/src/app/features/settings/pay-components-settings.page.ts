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
    <div class="mx-auto flex w-full max-w-5xl flex-col gap-5">
      <header class="flex flex-col gap-3 text-start sm:flex-row sm:items-end sm:justify-between">
        <div class="flex min-w-0 flex-col gap-2">
          <h1 class="text-2xl font-bold leading-tight text-fg">
            {{ 'settings.payComponents.title' | transloco }}
          </h1>
          <p class="text-sm text-fg-2">{{ 'settings.payComponents.subtitle' | transloco }}</p>
        </div>
        @if (canMutate()) {
          <kadr-button variant="primary" type="button" (click)="openCreate()">
            {{ 'settings.payComponents.add' | transloco }}
          </kadr-button>
        }
      </header>

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

      @if (state() === 'loading') {
        <kadr-skeleton height="12rem" />
      } @else if (state() === 'error') {
        <kadr-network-error
          [title]="'settings.payComponents.loadErrorTitle' | transloco"
          [description]="'settings.payComponents.loadErrorBody' | transloco"
          (retry)="reload()"
        />
      } @else if (items().length === 0) {
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
        <ul class="flex flex-col gap-2">
          @for (item of items(); track item.id) {
            <li
              class="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4 text-start sm:flex-row sm:items-center sm:justify-between"
            >
              <div class="min-w-0">
                <p class="text-sm font-medium text-fg">{{ displayName(item) }}</p>
                <p class="mt-1 text-xs text-fg-muted">
                  <span class="font-mono">{{ item.key }}</span>
                  · {{ kindLabel(item.kind) }}
                  · {{ calcLabel(item.calcType) }}
                  ·
                  {{
                    item.isActive
                      ? ('ui.statusActive' | transloco)
                      : ('settings.payComponents.inactive' | transloco)
                  }}
                </p>
              </div>
              <div class="flex flex-wrap gap-2">
                @if (canMutate()) {
                  <kadr-button variant="secondary" type="button" (click)="openEdit(item)">
                    {{ 'settings.payComponents.edit' | transloco }}
                  </kadr-button>
                  @if (item.isActive) {
                    <kadr-button
                      variant="danger-soft"
                      type="button"
                      (click)="askDeactivate(item)"
                    >
                      {{ 'settings.payComponents.deactivate' | transloco }}
                    </kadr-button>
                  }
                }
              </div>
            </li>
          }
        </ul>
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

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<PayComponent[]>([]);
  protected readonly query = signal('');
  protected readonly statusFilter = signal<'active' | 'inactive' | 'all'>('active');
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

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('settings.payComponents.title'));
    void this.reload();
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
