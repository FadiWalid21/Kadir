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
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { TemplateItem, TemplateSection } from '../../core/api/tenant-templates.api';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { TableComponent, TableColumn } from '../../shared/ui/table/table.component';
import { TabsComponent, TabItem } from '../../shared/ui/tabs/tabs.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { SettingsSubnavComponent } from './settings-subnav.component';
import { TemplatesStore } from './templates.store';

@Component({
  selector: 'kadr-templates-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [TemplatesStore],
  imports: [
    FormsModule,
    TranslocoPipe,
    SettingsSubnavComponent,
    TabsComponent,
    TableComponent,
    DrawerComponent,
    ButtonComponent,
    InputComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-5xl flex-col gap-5">
      <kadr-settings-subnav active="templates" [showTemplates]="canManage()" />

      <header class="flex flex-col gap-2 text-start">
        <h1 class="text-2xl font-bold leading-tight text-fg">
          {{ 'settings.templates.title' | transloco }}
        </h1>
        <p class="text-sm text-fg-2">{{ 'settings.templates.subtitle' | transloco }}</p>
      </header>

      @if (store.overviewState() === 'loading') {
        <div class="flex flex-col gap-3" role="status" [attr.aria-label]="'ui.loading' | transloco">
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="12rem" />
        </div>
      } @else if (store.overviewState() === 'error') {
        <kadr-empty-state
          [title]="'settings.templates.loadErrorTitle' | transloco"
          [description]="'settings.templates.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">
            {{ 'home.retry' | transloco }}
          </kadr-button>
        </kadr-empty-state>
      } @else {
        <section class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm sm:p-5">
          <h2 class="text-base font-bold text-fg">{{ 'settings.templates.industryTitle' | transloco }}</h2>
          <p class="mt-1 text-sm text-fg">{{ industryName() }}</p>
          <p class="mt-2 text-xs text-fg-2">{{ 'settings.templates.industryPermanent' | transloco }}</p>
          <p class="mt-1 text-xs text-fg-muted">
            {{ 'settings.templates.versionLabel' | transloco: { applied: store.overview()?.appliedVersion, latest: store.overview()?.latestVersion } }}
          </p>
        </section>

        @if (store.updateAvailable()) {
          <section
            class="flex flex-col gap-3 rounded-lg border border-line bg-info-soft p-4 text-start sm:flex-row sm:items-center sm:justify-between"
            role="status"
          >
            <div>
              <p class="text-sm font-medium text-info">{{ 'settings.templates.updateTitle' | transloco }}</p>
              <p class="mt-1 text-xs text-fg-2">{{ 'settings.templates.updateBody' | transloco }}</p>
            </div>
            <kadr-button
              variant="primary"
              type="button"
              [disabled]="!canManage() || store.busy()"
              [loading]="store.busy()"
              (click)="onAddMissing()"
            >
              {{ 'settings.templates.addMissing' | transloco }}
            </kadr-button>
          </section>
        }

        <kadr-tabs
          [tabs]="tabItems()"
          [ariaLabel]="'settings.templates.tabsAria' | transloco"
          [(activeId)]="activeTab"
          (activeChange)="onTabChange($event)"
        />

        <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
          <div class="min-w-0 flex-1">
            <kadr-input
              [label]="'settings.templates.search' | transloco"
              [value]="search()"
              (valueChange)="onSearch($event)"
            />
          </div>
        </div>

        @if (store.listState() === 'loading') {
          <kadr-skeleton height="10rem" />
        } @else if (store.listState() === 'error') {
          <kadr-empty-state
            [title]="'settings.templates.listErrorTitle' | transloco"
            [description]="'settings.templates.listErrorBody' | transloco"
          />
        } @else if (store.items().length === 0) {
          <kadr-empty-state
            [title]="'settings.templates.emptyTitle' | transloco"
            [description]="'settings.templates.emptyBody' | transloco"
          />
        } @else {
          <kadr-table [columns]="columns()">
            @for (item of store.items(); track item.id) {
              <tr class="border-b border-line last:border-b-0 hover:bg-subtle/60">
                <td class="px-3 py-2.5 text-start text-fg">
                  <button
                    type="button"
                    class="text-start font-medium text-fg underline-offset-2 hover:underline focus-visible:outline-none"
                    (click)="openItem(item)"
                  >
                    {{ displayName(item) }}
                  </button>
                </td>
                <td class="px-3 py-2.5">
                  @if (item.isCustomized) {
                    <kadr-badge tone="accent" [label]="'settings.templates.customized' | transloco" />
                  } @else {
                    <span class="text-xs text-fg-muted">{{ 'settings.templates.default' | transloco }}</span>
                  }
                </td>
                <td class="px-3 py-2.5">
                  <label class="inline-flex items-center gap-2 text-sm text-fg-2">
                    <input
                      type="checkbox"
                      class="size-4 rounded border-line"
                      [checked]="item.isActive"
                      [disabled]="!canManage() || store.busy()"
                      (change)="onToggle(item, $event)"
                    />
                    {{ item.isActive ? ('ui.statusActive' | transloco) : ('settings.templates.inactive' | transloco) }}
                  </label>
                </td>
                <td class="px-3 py-2.5">
                  <div class="flex flex-wrap gap-2">
                    <kadr-button variant="ghost" size="sm" type="button" (click)="openItem(item)">
                      {{ 'settings.templates.edit' | transloco }}
                    </kadr-button>
                    @if (item.isCustomized && canManage()) {
                      <kadr-button
                        variant="danger-soft"
                        size="sm"
                        type="button"
                        [disabled]="store.busy()"
                        (click)="onReset(item)"
                      >
                        {{ 'settings.templates.reset' | transloco }}
                      </kadr-button>
                    }
                  </div>
                </td>
              </tr>
            }
          </kadr-table>
        }
      }
    </div>

    <kadr-drawer
      [open]="!!store.drawerItem()"
      [title]="'settings.templates.editTitle' | transloco"
      [closeLabel]="'ui.cancel' | transloco"
      [hasFooter]="false"
      (closed)="store.closeDrawer()"
    >
      @if (store.drawerItem(); as item) {
        <form class="flex flex-col gap-4" (ngSubmit)="onSave()">
          <kadr-input
            [label]="'settings.templates.nameAr' | transloco"
            [value]="editNameAr()"
            (valueChange)="editNameAr.set($event)"
          />
          <kadr-input
            [label]="'settings.templates.nameEn' | transloco"
            [value]="editNameEn()"
            (valueChange)="editNameEn.set($event)"
          />
          @if (store.section() === 'shifts') {
            <kadr-input
              [label]="'settings.templates.startTime' | transloco"
              [value]="editStart()"
              (valueChange)="editStart.set($event)"
            />
            <kadr-input
              [label]="'settings.templates.endTime' | transloco"
              [value]="editEnd()"
              (valueChange)="editEnd.set($event)"
            />
            <kadr-input
              [label]="'settings.templates.graceMinutes' | transloco"
              [value]="editGrace()"
              (valueChange)="editGrace.set($event)"
            />
          }
          @if (store.section() === 'leave-types') {
            <label class="inline-flex items-center gap-2 text-sm text-fg">
              <input type="checkbox" class="size-4" [checked]="editPaid()" (change)="editPaid.set($any($event.target).checked)" />
              {{ 'settings.templates.paid' | transloco }}
            </label>
            <kadr-input
              [label]="'settings.templates.daysPerYear' | transloco"
              [value]="editDays()"
              (valueChange)="editDays.set($event)"
            />
            <p class="text-xs text-fg-muted">{{ 'settings.templates.verifyDisclaimer' | transloco }}</p>
          }
          @if (store.section() === 'document-types') {
            <label class="inline-flex items-center gap-2 text-sm text-fg">
              <input type="checkbox" class="size-4" [checked]="editHasExpiry()" (change)="editHasExpiry.set($any($event.target).checked)" />
              {{ 'settings.templates.hasExpiry' | transloco }}
            </label>
          }
          <div class="mt-2 flex flex-wrap justify-end gap-2">
            <kadr-button variant="secondary" type="button" (click)="store.closeDrawer()">
              {{ 'ui.cancel' | transloco }}
            </kadr-button>
            <kadr-button variant="primary" type="submit" [disabled]="!canManage()" [loading]="store.busy()">
              {{ 'ui.save' | transloco }}
            </kadr-button>
          </div>
        </form>
      }
    </kadr-drawer>
  `,
})
export class TemplatesSettingsPage implements OnInit {
  protected readonly store = inject(TemplatesStore);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  protected readonly canManage = computed(() =>
    this.auth.hasPermission(PermissionCodes.TenantTemplatesManage),
  );

  protected readonly activeTab = signal<string>('shifts');
  protected readonly search = signal('');
  protected readonly editNameAr = signal('');
  protected readonly editNameEn = signal('');
  protected readonly editStart = signal('09:00');
  protected readonly editEnd = signal('17:00');
  protected readonly editGrace = signal('15');
  protected readonly editPaid = signal(true);
  protected readonly editDays = signal('');
  protected readonly editHasExpiry = signal(false);

  protected readonly tabItems = computed<TabItem[]>(() => [
    { id: 'shifts', label: this.transloco.translate('settings.templates.tabs.shifts') },
    { id: 'leave-types', label: this.transloco.translate('settings.templates.tabs.leaveTypes') },
    { id: 'document-types', label: this.transloco.translate('settings.templates.tabs.documentTypes') },
    { id: 'job-titles', label: this.transloco.translate('settings.templates.tabs.jobTitles') },
    { id: 'departments', label: this.transloco.translate('settings.templates.tabs.departments') },
  ]);

  protected readonly columns = computed<TableColumn[]>(() => [
    { id: 'name', label: this.transloco.translate('settings.templates.colName') },
    { id: 'custom', label: this.transloco.translate('settings.templates.colStatus') },
    { id: 'active', label: this.transloco.translate('settings.templates.colActive') },
    { id: 'actions', label: this.transloco.translate('settings.templates.colActions') },
  ]);

  protected readonly industryName = computed(() => {
    const o = this.store.overview();
    if (!o) {
      return '';
    }
    return this.transloco.getActiveLang() === 'en' ? o.industryNameEn : o.industryNameAr;
  });

  ngOnInit(): void {
    void this.reload();
  }

  openItem(item: TemplateItem): void {
    this.editNameAr.set(item.nameAr);
    this.editNameEn.set(item.nameEn);
    this.editStart.set(String(item.fields['startTime'] ?? '09:00'));
    this.editEnd.set(String(item.fields['endTime'] ?? '17:00'));
    this.editGrace.set(String(item.fields['graceMinutes'] ?? 15));
    this.editPaid.set(Boolean(item.fields['paid'] ?? true));
    const days = item.fields['daysPerYear'];
    this.editDays.set(days === null || days === undefined ? '' : String(days));
    this.editHasExpiry.set(Boolean(item.fields['hasExpiry'] ?? false));
    this.store.openDrawer(item);
  }

  async reload(): Promise<void> {
    await this.store.loadOverview();
    await this.store.loadSection(this.activeTab() as TemplateSection);
  }

  onTabChange(id: string): void {
    void this.store.loadSection(id as TemplateSection);
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.store.query.set(value);
    void this.store.loadSection();
  }

  displayName(item: TemplateItem): string {
    return this.transloco.getActiveLang() === 'en' ? item.nameEn : item.nameAr;
  }

  async onAddMissing(): Promise<void> {
    const added = await this.store.addMissing();
    if (added < 0) {
      this.toast.show(this.transloco.translate('settings.templates.toastFail'), 'danger');
      return;
    }
    this.toast.show(
      this.transloco.translate('settings.templates.toastAdded', { count: added }),
      'success',
    );
  }

  async onToggle(item: TemplateItem, event: Event): Promise<void> {
    const checked = (event.target as HTMLInputElement).checked;
    const ok = await this.store.toggleActive(item, checked);
    if (!ok) {
      this.toast.show(this.transloco.translate('settings.templates.toastFail'), 'danger');
    }
  }

  async onReset(item: TemplateItem): Promise<void> {
    if (!confirm(this.transloco.translate('settings.templates.resetConfirm'))) {
      return;
    }
    const ok = await this.store.resetItem(item);
    this.toast.show(
      this.transloco.translate(ok ? 'settings.templates.toastReset' : 'settings.templates.toastFail'),
      ok ? 'success' : 'danger',
    );
  }

  async onSave(): Promise<void> {
    const section = this.store.section();
    let body: unknown;
    if (section === 'shifts') {
      body = {
        nameAr: this.editNameAr(),
        nameEn: this.editNameEn(),
        startTime: this.editStart(),
        endTime: this.editEnd(),
        graceMinutes: Number(this.editGrace()) || 0,
      };
    } else if (section === 'leave-types') {
      const days = this.editDays().trim();
      body = {
        nameAr: this.editNameAr(),
        nameEn: this.editNameEn(),
        paid: this.editPaid(),
        daysPerYear: days === '' ? null : Number(days),
        requiresApproval: true,
        requiresAttachment: false,
      };
    } else if (section === 'document-types') {
      const item = this.store.drawerItem();
      body = {
        nameAr: this.editNameAr(),
        nameEn: this.editNameEn(),
        hasExpiry: this.editHasExpiry(),
        alertDays: (item?.fields['alertDays'] as number[] | undefined) ?? [],
      };
    } else {
      body = { nameAr: this.editNameAr(), nameEn: this.editNameEn() };
    }

    const ok = await this.store.saveDrawer(body);
    this.toast.show(
      this.transloco.translate(ok ? 'settings.templates.toastSaved' : 'settings.templates.toastFail'),
      ok ? 'success' : 'danger',
    );
  }
}
