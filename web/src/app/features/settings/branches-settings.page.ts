import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  BranchListItem,
  BranchSettingsApi,
  BranchWriteBody,
} from '../../core/api/branch-settings.api';
import { LookupsApi } from '../../core/api/lookups.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import {
  ComboboxComponent,
  ComboboxOption,
  ComboboxSearchFn,
} from '../../shared/ui/combobox/combobox.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { TableComponent, TableColumn } from '../../shared/ui/table/table.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-branches-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    SkeletonComponent,
    EmptyStateComponent,
    TableComponent,
    DrawerComponent,
    ComboboxComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-5xl flex-col gap-5">
      <header class="flex flex-col gap-3 text-start sm:flex-row sm:items-end sm:justify-between">
        <div class="flex min-w-0 flex-col gap-2">
          <h1 class="text-2xl font-bold leading-tight text-fg">
            {{ 'settings.branches.title' | transloco }}
          </h1>
          <p class="text-sm text-fg-2">{{ 'settings.branches.subtitle' | transloco }}</p>
        </div>
        @if (canMutate()) {
          <kadr-button variant="primary" type="button" (click)="openCreate()">
            {{ 'settings.branches.add' | transloco }}
          </kadr-button>
        }
      </header>

      <div class="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div class="min-w-0 flex-1">
          <kadr-input
            [label]="'settings.branches.search' | transloco"
            [value]="query()"
            (valueChange)="onSearch($event)"
          />
        </div>
        <label class="flex flex-col gap-2 text-sm text-fg">
          <span class="font-medium">{{ 'settings.branches.statusFilter' | transloco }}</span>
          <select
            class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
            [value]="statusFilter()"
            (change)="onStatusFilter($any($event.target).value)"
          >
            <option value="all">{{ 'settings.branches.statusAll' | transloco }}</option>
            <option value="active">{{ 'ui.statusActive' | transloco }}</option>
            <option value="inactive">{{ 'settings.branches.inactive' | transloco }}</option>
          </select>
        </label>
      </div>

      @if (state() === 'loading') {
        <kadr-skeleton height="12rem" />
      } @else if (state() === 'error') {
        <kadr-empty-state
          [title]="'settings.branches.loadErrorTitle' | transloco"
          [description]="'settings.branches.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">
            {{ 'home.retry' | transloco }}
          </kadr-button>
        </kadr-empty-state>
      } @else if (items().length === 0) {
        <kadr-empty-state
          [title]="'settings.branches.emptyTitle' | transloco"
          [description]="'settings.branches.emptyBody' | transloco"
        >
          @if (canMutate()) {
            <kadr-button variant="primary" type="button" (click)="openCreate()">
              {{ 'settings.branches.add' | transloco }}
            </kadr-button>
          }
        </kadr-empty-state>
      } @else {
        <kadr-table [columns]="columns()">
          @for (item of items(); track item.id) {
            <tr class="border-b border-line last:border-b-0 hover:bg-subtle/60">
              <td class="px-3 py-2.5 text-start text-fg">
                <button
                  type="button"
                  class="text-start font-medium underline-offset-2 hover:underline focus-visible:outline-none"
                  (click)="openEdit(item)"
                >
                  {{ displayName(item) }}
                </button>
              </td>
              <td class="px-3 py-2.5 text-start text-sm text-fg-2">{{ item.legalEntityName }}</td>
              <td class="px-3 py-2.5 text-start text-sm text-fg-2">
                {{ item.address || ('settings.branches.noAddress' | transloco) }}
              </td>
              <td class="px-3 py-2.5">
                <label class="inline-flex items-center gap-2 text-sm text-fg-2">
                  <input
                    type="checkbox"
                    class="size-4 rounded border-line"
                    [checked]="item.isActive"
                    [disabled]="!canMutate() || busy()"
                    (change)="onToggle(item, $event)"
                  />
                  {{
                    item.isActive
                      ? ('ui.statusActive' | transloco)
                      : ('settings.branches.inactive' | transloco)
                  }}
                </label>
              </td>
              <td class="px-3 py-2.5">
                <kadr-button variant="ghost" size="sm" type="button" (click)="openEdit(item)">
                  {{ 'settings.branches.edit' | transloco }}
                </kadr-button>
              </td>
            </tr>
          }
        </kadr-table>
      }
    </div>

    <kadr-drawer
      [open]="drawerOpen()"
      [title]="(mode() === 'create' ? 'settings.branches.addTitle' : 'settings.branches.editTitle') | transloco"
      [closeLabel]="'ui.cancel' | transloco"
      [hasFooter]="false"
      (closed)="closeDrawer()"
    >
      <form class="flex flex-col gap-4" (ngSubmit)="onSave()">
        <kadr-input
          [label]="'settings.branches.nameAr' | transloco"
          [value]="editNameAr()"
          (valueChange)="editNameAr.set($event)"
        />
        <kadr-input
          [label]="'settings.branches.nameEn' | transloco"
          [value]="editNameEn()"
          (valueChange)="editNameEn.set($event)"
        />
        <kadr-combobox
          [label]="'settings.branches.legalEntity' | transloco"
          [searchFn]="legalEntitySearch"
          [value]="editLegalEntityId()"
          [displayName]="editLegalEntityName()"
          (selectionChange)="onLegalEntitySelected($event)"
        />
        <kadr-input
          [label]="'settings.branches.address' | transloco"
          [value]="editAddress()"
          (valueChange)="editAddress.set($event)"
        />
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
  `,
})
export class BranchesSettingsPage implements OnInit {
  private readonly api = inject(BranchSettingsApi);
  private readonly lookups = inject(LookupsApi);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<BranchListItem[]>([]);
  protected readonly query = signal('');
  protected readonly statusFilter = signal<'all' | 'active' | 'inactive'>('all');
  protected readonly busy = signal(false);
  protected readonly drawerOpen = signal(false);
  protected readonly mode = signal<'create' | 'edit'>('create');
  protected readonly editingId = signal<string | null>(null);
  protected readonly editNameAr = signal('');
  protected readonly editNameEn = signal('');
  protected readonly editAddress = signal('');
  protected readonly editLegalEntityId = signal('');
  protected readonly editLegalEntityName = signal('');

  protected readonly canMutate = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.TenantSettingsUpdate) &&
      !this.subscription.readOnly(),
  );

  protected readonly columns = computed<TableColumn[]>(() => [
    { id: 'name', label: this.transloco.translate('settings.branches.colName') },
    { id: 'entity', label: this.transloco.translate('settings.branches.colLegalEntity') },
    { id: 'address', label: this.transloco.translate('settings.branches.colAddress') },
    { id: 'active', label: this.transloco.translate('settings.branches.colActive') },
    { id: 'actions', label: this.transloco.translate('settings.branches.colActions') },
  ]);

  protected readonly legalEntitySearch: ComboboxSearchFn = async (q) => {
    const page = await this.lookups.searchLegalEntities(q, 1, 20);
    return page.items.map((i) => ({ id: i.id, name: i.name }));
  };

  ngOnInit(): void {
    void this.reload();
  }

  displayName(item: BranchListItem): string {
    return this.transloco.getActiveLang() === 'en' ? item.nameEn : item.nameAr;
  }

  async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const active =
        this.statusFilter() === 'all' ? null : this.statusFilter() === 'active';
      const page = await this.api.list({
        page: 1,
        pageSize: 100,
        q: this.query() || undefined,
        active,
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

  async openCreate(): Promise<void> {
    this.mode.set('create');
    this.editingId.set(null);
    this.editNameAr.set('');
    this.editNameEn.set('');
    this.editAddress.set('');
    await this.prefillLegalEntity();
    this.drawerOpen.set(true);
  }

  openEdit(item: BranchListItem): void {
    this.mode.set('edit');
    this.editingId.set(item.id);
    this.editNameAr.set(item.nameAr);
    this.editNameEn.set(item.nameEn);
    this.editAddress.set(item.address ?? '');
    this.editLegalEntityId.set(item.legalEntityId);
    this.editLegalEntityName.set(item.legalEntityName);
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  onLegalEntitySelected(option: ComboboxOption | null): void {
    this.editLegalEntityId.set(option?.id ?? '');
    this.editLegalEntityName.set(option?.name ?? '');
  }

  async onToggle(item: BranchListItem, event: Event): Promise<void> {
    const checked = (event.target as HTMLInputElement).checked;
    this.busy.set(true);
    try {
      const updated = await this.api.setActive(item.id, checked);
      this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
    } catch (err) {
      (event.target as HTMLInputElement).checked = item.isActive;
      const code = problemDetailsFromError(err)?.code;
      const key =
        code === 'branches.last_active'
          ? 'settings.branches.toastLastActive'
          : 'settings.branches.toastFail';
      this.toast.show(this.transloco.translate(key), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  async onSave(): Promise<void> {
    const legalEntityId = this.editLegalEntityId().trim();
    if (!legalEntityId) {
      this.toast.show(this.transloco.translate('settings.branches.toastLegalEntityRequired'), 'danger');
      return;
    }
    const body: BranchWriteBody = {
      legalEntityId,
      nameAr: this.editNameAr().trim(),
      nameEn: this.editNameEn().trim(),
      address: this.editAddress().trim() || null,
    };
    this.busy.set(true);
    try {
      if (this.mode() === 'create') {
        const created = await this.api.create(body);
        this.items.update((list) => [created, ...list]);
      } else {
        const id = this.editingId();
        if (!id) {
          return;
        }
        const updated = await this.api.update(id, body);
        this.items.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
      }
      this.closeDrawer();
      this.toast.show(this.transloco.translate('settings.branches.toastSaved'), 'success');
    } catch (err) {
      const code = problemDetailsFromError(err)?.code;
      const key =
        code === 'branches.duplicate_name'
          ? 'settings.branches.toastDuplicate'
          : 'settings.branches.toastFail';
      this.toast.show(this.transloco.translate(key), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  private async prefillLegalEntity(): Promise<void> {
    try {
      const page = await this.lookups.searchLegalEntities('', 1, 2);
      if (page.items.length === 1) {
        this.editLegalEntityId.set(page.items[0]!.id);
        this.editLegalEntityName.set(page.items[0]!.name);
      } else {
        this.editLegalEntityId.set('');
        this.editLegalEntityName.set('');
      }
    } catch {
      this.editLegalEntityId.set('');
      this.editLegalEntityName.set('');
    }
  }
}
