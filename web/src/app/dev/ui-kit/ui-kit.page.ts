import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ThemeService } from '../../core/theme/theme.service';
import type { AppLanguage, DensityMode, ThemeMode } from '../../core/theme/theme.types';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { CardComponent } from '../../shared/ui/card/card.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { DropdownMenuComponent } from '../../shared/ui/dropdown-menu/dropdown-menu.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { CheckboxComponent } from '../../shared/ui/checkbox/checkbox.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { SeatLimitDialogComponent } from '../../shared/ui/seat-limit-dialog/seat-limit-dialog.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { RadioCardComponent } from '../../shared/ui/radio-card/radio-card.component';
import { ComboboxComponent } from '../../shared/ui/combobox/combobox.component';
import {
  AssignmentTargetPickerComponent,
  AssignmentTargetSelection,
} from '../../shared/ui/assignment-target-picker/assignment-target-picker.component';
import { SelectComponent } from '../../shared/ui/select/select.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { StepperComponent } from '../../shared/ui/stepper/stepper.component';
import { TableComponent } from '../../shared/ui/table/table.component';
import { TabsComponent } from '../../shared/ui/tabs/tabs.component';
import { ToastHostComponent } from '../../shared/ui/toast/toast.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { InstallBannerComponent } from '../../shared/ui/pwa/install-banner.component';
import { IosInstallSheetComponent } from '../../shared/ui/pwa/ios-install-sheet.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { OfflineBannerComponent } from '../../shared/ui/pwa/offline-banner.component';
import { UpdateNoticeComponent } from '../../shared/ui/pwa/update-notice.component';
import { OfflineService } from '../../core/pwa/offline.service';
import { UpdateService } from '../../core/pwa/update.service';
import { InstallPromptService } from '../../core/pwa/install-prompt.service';
import { DirtyFormRegistry } from '../../core/forms/dirty-form.registry';

const SWATCHES = [
  '--brand-50',
  '--brand-100',
  '--brand-200',
  '--brand-600',
  '--brand-700',
  '--brand-800',
  '--accent',
  '--accent-soft',
  '--bg-app',
  '--bg-surface',
  '--bg-subtle',
  '--border',
  '--text-primary',
  '--text-secondary',
  '--success',
  '--warning',
  '--danger',
  '--info',
] as const;

@Component({
  selector: 'kadr-ui-kit-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    BadgeComponent,
    CardComponent,
    AvatarComponent,
    DropdownMenuComponent,
    EmptyStateComponent,
    PageHeaderComponent,
    SkeletonComponent,
    ToastHostComponent,
    IconComponent,
    StepperComponent,
    TabsComponent,
    DrawerComponent,
    TableComponent,
    SelectComponent,
    ComboboxComponent,
    AssignmentTargetPickerComponent,
    CheckboxComponent,
    RadioCardComponent,
    ConfirmDialogComponent,
    SeatLimitDialogComponent,
    OfflineBannerComponent,
    UpdateNoticeComponent,
    InstallBannerComponent,
    NetworkErrorComponent,
    IosInstallSheetComponent,
  ],
  template: `
    <div class="min-h-dvh bg-app text-fg">
      <kadr-toast-host />
      <div
        class="sticky top-0 z-10 flex flex-wrap items-end gap-3 border-b border-line bg-surface px-4 py-3"
      >
        <label class="grid gap-1 text-xs text-fg-2">
          <span>{{ 'uiKit.language' | transloco }}</span>
          <select
            class="h-9 rounded-sm border border-line-strong bg-surface px-2 text-sm text-fg"
            [ngModel]="theme.language()"
            (ngModelChange)="onLanguage($event)"
          >
            <option value="ar">{{ 'uiKit.langAr' | transloco }}</option>
            <option value="en">{{ 'uiKit.langEn' | transloco }}</option>
          </select>
        </label>
        <label class="grid gap-1 text-xs text-fg-2">
          <span>{{ 'uiKit.theme' | transloco }}</span>
          <select
            class="h-9 rounded-sm border border-line-strong bg-surface px-2 text-sm text-fg"
            [ngModel]="theme.theme()"
            (ngModelChange)="onTheme($event)"
          >
            <option value="light">{{ 'uiKit.themeLight' | transloco }}</option>
            <option value="dark">{{ 'uiKit.themeDark' | transloco }}</option>
            <option value="system">{{ 'uiKit.themeSystem' | transloco }}</option>
          </select>
        </label>
        <label class="grid gap-1 text-xs text-fg-2">
          <span>{{ 'uiKit.density' | transloco }}</span>
          <select
            class="h-9 rounded-sm border border-line-strong bg-surface px-2 text-sm text-fg"
            [ngModel]="theme.density()"
            (ngModelChange)="onDensity($event)"
          >
            <option value="comfortable">{{ 'uiKit.densityComfortable' | transloco }}</option>
            <option value="compact">{{ 'uiKit.densityCompact' | transloco }}</option>
          </select>
        </label>
        <label class="grid gap-1 text-xs text-fg-2">
          <span>{{ 'uiKit.brand' | transloco }}</span>
          <input
            type="color"
            class="h-9 w-12 cursor-pointer rounded-sm border border-line-strong bg-surface p-1"
            [value]="theme.brandHex()"
            (input)="onBrand($event)"
          />
        </label>
      </div>

      <main class="mx-auto grid max-w-5xl gap-5 p-6">
        <header class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 class="text-2xl font-bold leading-tight">{{ 'uiKit.title' | transloco }}</h1>
            <p class="text-sm text-fg-2">{{ 'uiKit.subtitle' | transloco }}</p>
            <p class="mt-1 font-medium text-fg">{{ 'uiKit.sampleCompany' | transloco }}</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <kadr-button variant="secondary">{{ 'ui.export' | transloco }}</kadr-button>
            <kadr-button variant="primary">{{ 'ui.addEmployee' | transloco }}</kadr-button>
          </div>
        </header>

        @if (theme.brandNearDanger()) {
          <p class="rounded-md border border-line bg-warning-soft px-3 py-2 text-sm text-warning" role="status">
            {{ 'uiKit.nearDanger' | transloco }}
          </p>
        }

        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div class="rounded-md border border-line bg-surface p-4">
            <span class="text-sm text-fg-2">{{ 'uiKit.statEmployees' | transloco }}</span>
            <strong class="mt-1 block text-xl font-bold tabular-nums">128</strong>
          </div>
          <div class="rounded-md border border-line bg-surface p-4">
            <span class="text-sm text-fg-2">{{ 'uiKit.statPresent' | transloco }}</span>
            <strong class="mt-1 block text-xl font-bold tabular-nums">109</strong>
          </div>
          <div class="rounded-md border border-line bg-surface p-4">
            <span class="text-sm text-fg-2">{{ 'uiKit.statLate' | transloco }}</span>
            <strong class="mt-1 block text-xl font-bold tabular-nums">7</strong>
          </div>
          <div class="rounded-md border border-line bg-surface p-4">
            <span class="text-sm text-fg-2">{{ 'uiKit.statPending' | transloco }}</span>
            <strong class="mt-1 block text-xl font-bold tabular-nums">12</strong>
          </div>
        </div>

        <kadr-card [title]="'uiKit.buttons' | transloco">
          <div class="flex flex-wrap items-center gap-2">
            <kadr-button variant="primary">{{ 'ui.save' | transloco }}</kadr-button>
            <kadr-button variant="secondary">{{ 'ui.cancel' | transloco }}</kadr-button>
            <kadr-button variant="danger-soft">{{ 'ui.delete' | transloco }}</kadr-button>
            <kadr-button variant="ghost">{{ 'ui.skip' | transloco }}</kadr-button>
            <kadr-button variant="primary" [loading]="true">{{ 'ui.loading' | transloco }}</kadr-button>
            <kadr-button
              variant="secondary"
              size="sm"
              ariaLabel="icon only"
            >
              <svg class="size-4 rtl-flip" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                <path d="m9 18 6-6-6-6" />
              </svg>
            </kadr-button>
          </div>
        </kadr-card>

        <kadr-card [title]="'uiKit.inputs' | transloco">
          <div class="grid gap-4 md:grid-cols-2">
            <kadr-input
              [label]="'ui.email' | transloco"
              [hint]="'ui.emailHint' | transloco"
              value="name@company.com"
            />
            <kadr-input
              [label]="'ui.email' | transloco"
              [error]="'ui.emailError' | transloco"
              value="bad"
            />
          </div>
        </kadr-card>

        <kadr-card [title]="'uiKit.badges' | transloco">
          <div class="flex flex-wrap gap-2">
            <kadr-badge tone="success" [label]="'ui.statusApproved' | transloco" />
            <kadr-badge tone="warning" [label]="'ui.statusPending' | transloco" />
            <kadr-badge tone="danger" [label]="'ui.statusRejected' | transloco" />
            <kadr-badge tone="info" [label]="'ui.statusNew' | transloco" />
            <kadr-badge tone="accent" [label]="'ui.statusProbation' | transloco" />
          </div>
        </kadr-card>

        <kadr-card [title]="'uiKit.shell' | transloco">
          <div class="flex flex-col gap-4">
            <div class="flex flex-wrap gap-3 text-fg">
              <kadr-icon name="home" />
              <kadr-icon name="users" />
              <kadr-icon name="clock" />
              <kadr-icon name="calendar" />
              <kadr-icon name="inbox" />
              <kadr-icon name="chart" />
              <kadr-icon name="settings" />
              <kadr-icon name="search" />
              <kadr-icon name="bell" />
            </div>
            <div>
              <p class="mb-2 text-xs text-fg-2">{{ 'uiKit.stepper' | transloco }}</p>
              <kadr-stepper
                [steps]="stepperDemo"
                [currentIndex]="1"
                [ariaLabel]="'uiKit.stepper' | transloco"
              />
            </div>
            <div>
              <p class="mb-2 text-xs text-fg-2">{{ 'uiKit.avatar' | transloco }}</p>
              <div class="flex items-center gap-2">
                <kadr-avatar name="أحمد محمد" size="sm" />
                <kadr-avatar name="شركة النور" size="md" />
                <kadr-avatar name="Sara" size="lg" />
              </div>
            </div>
            <div>
              <p class="mb-2 text-xs text-fg-2">{{ 'uiKit.pageHeader' | transloco }}</p>
              <kadr-page-header
                [title]="'uiKit.sampleCompany' | transloco"
                [subtitle]="'uiKit.subtitle' | transloco"
              />
            </div>
            <div>
              <p class="mb-2 text-xs text-fg-2">{{ 'uiKit.dropdown' | transloco }}</p>
              <kadr-dropdown-menu [ariaLabel]="'uiKit.dropdown' | transloco">
                <span dropdownTrigger class="rounded-md border border-line px-3 py-2 text-sm">
                  {{ 'shell.userMenu' | transloco }}
                </span>
                <div dropdownContent>
                  <button type="button" class="block w-full rounded-sm px-3 py-2 text-start text-sm hover:bg-hover" role="menuitem">
                    {{ 'auth.signOut' | transloco }}
                  </button>
                </div>
              </kadr-dropdown-menu>
            </div>
            <div>
              <p class="mb-2 text-xs text-fg-2">{{ 'uiKit.skeleton' | transloco }}</p>
              <kadr-skeleton height="2rem" width="50%" />
            </div>
            <div>
              <p class="mb-2 text-xs text-fg-2">{{ 'uiKit.emptyState' | transloco }}</p>
              <kadr-empty-state
                [title]="'home.widgetApprovalsTitle' | transloco"
                [description]="'home.widgetEmptyBody' | transloco"
              />
            </div>
            <div>
              <p class="mb-2 text-xs text-fg-2">{{ 'uiKit.toast' | transloco }}</p>
              <kadr-button variant="secondary" type="button" (click)="showToast()">
                {{ 'uiKit.showToast' | transloco }}
              </kadr-button>
            </div>
          </div>
        </kadr-card>

        <kadr-card [title]="'uiKit.tabs' | transloco">
          <kadr-tabs
            [tabs]="demoTabs"
            [ariaLabel]="'uiKit.tabs' | transloco"
            [(activeId)]="demoTabId"
          />
          <p class="mt-3 text-sm text-fg-2">{{ demoTabId() }}</p>
        </kadr-card>

        <kadr-card [title]="'uiKit.drawer' | transloco">
          <kadr-button variant="secondary" type="button" (click)="drawerOpen.set(true)">
            {{ 'uiKit.openDrawer' | transloco }}
          </kadr-button>
          <kadr-drawer
            [open]="drawerOpen()"
            [title]="'uiKit.drawer' | transloco"
            [closeLabel]="'ui.cancel' | transloco"
            [hasFooter]="false"
            (closed)="drawerOpen.set(false)"
          >
            <p class="text-sm text-fg-2">{{ 'uiKit.drawerBody' | transloco }}</p>
          </kadr-drawer>
        </kadr-card>

        <kadr-card [title]="'uiKit.pwa' | transloco">
          <div class="flex flex-col gap-4">
            <div class="flex flex-wrap gap-2">
              <kadr-button variant="secondary" type="button" (click)="demoOffline()">
                {{ 'uiKit.pwaOfflineBanner' | transloco }}
              </kadr-button>
              <kadr-button variant="secondary" type="button" (click)="demoUpdate()">
                {{ 'uiKit.pwaUpdateNotice' | transloco }}
              </kadr-button>
              <kadr-button variant="secondary" type="button" (click)="demoIosSheet()">
                {{ 'uiKit.pwaIosSheet' | transloco }}
              </kadr-button>
            </div>
            <kadr-offline-banner />
            <kadr-update-notice />
            <p class="text-xs text-fg-2">{{ 'uiKit.pwaInstallBanner' | transloco }}</p>
            <kadr-install-banner />
            <p class="text-xs text-fg-2">{{ 'uiKit.pwaNetworkError' | transloco }}</p>
            <kadr-network-error (retry)="showToast()" />
            <kadr-ios-install-sheet />
          </div>
        </kadr-card>

        <kadr-card [title]="'uiKit.table' | transloco">
          <kadr-table [columns]="demoColumns">
            <tr class="border-b border-line">
              <td class="h-row px-3">{{ 'uiKit.sampleName1' | transloco }}</td>
              <td class="px-3">{{ 'uiKit.sampleRole1' | transloco }}</td>
              <td class="px-3"><kadr-badge tone="success" [label]="'ui.statusActive' | transloco" /></td>
            </tr>
            <tr class="border-b border-line">
              <td class="h-row px-3">{{ 'uiKit.sampleName2' | transloco }}</td>
              <td class="px-3">{{ 'uiKit.sampleRole2' | transloco }}</td>
              <td class="px-3"><kadr-badge tone="warning" [label]="'ui.statusLeave' | transloco" /></td>
            </tr>
          </kadr-table>
        </kadr-card>

        <kadr-card [title]="'uiKit.select' | transloco">
          <div class="grid gap-4 md:grid-cols-2">
            <kadr-select
              [label]="'uiKit.select' | transloco"
              [options]="demoSelectOptions"
              [value]="demoSelect()"
              (valueChange)="demoSelect.set($event)"
            />
            <kadr-combobox
              [label]="'uiKit.combobox' | transloco"
              [placeholder]="'ui.combobox.searchPlaceholder' | transloco"
              [searchFn]="demoComboboxSearch"
              [value]="demoComboboxId()"
              [displayName]="demoComboboxName()"
              (selectionChange)="onDemoCombobox($event)"
            />
            <div class="md:col-span-2">
              <p class="mb-2 text-sm font-medium text-fg">{{ 'uiKit.assignmentTarget' | transloco }}</p>
              <kadr-assignment-target-picker
                [allowedTypes]="['Employee', 'Department', 'Branch', 'JobTitle']"
                [multiple]="true"
                (selectionChange)="onDemoAssignmentTarget($event)"
              />
            </div>
            <kadr-checkbox
              [label]="'uiKit.checkbox' | transloco"
              [checked]="demoChecked()"
              (checkedChange)="demoChecked.set($event)"
            />
            <kadr-radio-card
              [title]="'uiKit.radioCard' | transloco"
              [description]="'uiKit.subtitle' | transloco"
              [selected]="demoRadio()"
              (selectedChange)="demoRadio.set(true)"
            />
            <div>
              <kadr-button variant="secondary" type="button" (click)="confirmOpen.set(true)">
                {{ 'uiKit.confirmDialog' | transloco }}
              </kadr-button>
              <kadr-confirm-dialog
                [open]="confirmOpen()"
                [title]="'uiKit.confirmDialog' | transloco"
                [message]="'uiKit.drawerBody' | transloco"
                [confirmLabel]="'ui.delete' | transloco"
                [cancelLabel]="'ui.cancel' | transloco"
                [danger]="true"
                (confirmed)="confirmOpen.set(false)"
                (cancelled)="confirmOpen.set(false)"
              />
            </div>
            <div>
              <kadr-button variant="secondary" type="button" (click)="seatOpen.set(true)">
                {{ 'subscription.seatLimit.title' | transloco }}
              </kadr-button>
              <kadr-seat-limit-dialog
                [open]="seatOpen()"
                [cap]="25"
                [active]="26"
                (closed)="seatOpen.set(false)"
              />
            </div>
          </div>
        </kadr-card>

        <kadr-card [title]="'uiKit.palette' | transloco">
          <div class="flex flex-wrap gap-2">
            @for (token of swatches; track token) {
              <figure class="m-0 text-center text-xs text-fg-2">
                <i
                  class="mb-1 block h-11 w-16 rounded-sm border border-line"
                  [style.background]="'var(' + token + ')'"
                ></i>
                {{ token.slice(2) }}
              </figure>
            }
          </div>
        </kadr-card>
      </main>
    </div>
  `,
})
export class UiKitPage {
  protected readonly theme = inject(ThemeService);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);
  private readonly offline = inject(OfflineService);
  private readonly updates = inject(UpdateService);
  private readonly install = inject(InstallPromptService);
  private readonly dirtyForms = inject(DirtyFormRegistry);
  protected readonly swatches = SWATCHES;
  protected readonly stepperDemo = [
    { id: 'a', label: '1' },
    { id: 'b', label: '2' },
    { id: 'c', label: '3' },
  ];
  protected readonly demoTabId = signal('one');
  protected readonly drawerOpen = signal(false);
  protected readonly demoSelect = signal('a');
  protected readonly demoChecked = signal(true);
  protected readonly demoRadio = signal(true);
  protected readonly confirmOpen = signal(false);
  protected readonly seatOpen = signal(false);
  protected readonly demoSelectOptions = [
    { value: 'a', label: 'A' },
    { value: 'b', label: 'B' },
  ];
  protected readonly demoComboboxId = signal('');
  protected readonly demoComboboxName = signal('');
  private readonly demoComboboxAll = [
    { id: '1', name: 'أحمد علي' },
    { id: '2', name: 'Sara Mohamed' },
    { id: '3', name: 'فرع القاهرة' },
  ];
  protected readonly demoComboboxSearch = async (q: string) => {
    const term = q.trim().toLowerCase();
    await new Promise((r) => setTimeout(r, 120));
    return this.demoComboboxAll.filter((x) => !term || x.name.toLowerCase().includes(term));
  };
  protected readonly demoTabs = [
    { id: 'one', label: '1' },
    { id: 'two', label: '2' },
    { id: 'three', label: '3' },
  ];
  protected readonly demoColumns = [
    { id: 'name', label: 'Name' },
    { id: 'role', label: 'Role' },
    { id: 'status', label: 'Status' },
  ];

  onDemoCombobox(opt: { id: string; name: string } | null): void {
    this.demoComboboxId.set(opt?.id ?? '');
    this.demoComboboxName.set(opt?.name ?? '');
  }

  onDemoAssignmentTarget(_selection: AssignmentTargetSelection): void {
    void _selection;
  }

  onLanguage(lang: AppLanguage): void {
    this.theme.setLanguage(lang);
    this.transloco.setActiveLang(lang);
  }

  onTheme(mode: ThemeMode): void {
    this.theme.setTheme(mode);
  }

  onDensity(density: DensityMode): void {
    this.theme.setDensity(density);
  }

  onBrand(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.theme.setBrand(value);
  }

  showToast(): void {
    this.toast.show(this.transloco.translate('shell.notAvailableYet'), 'info');
  }

  demoOffline(): void {
    if (this.offline.offline()) {
      this.offline.noteSuccessfulRequest();
    } else {
      this.offline.noteFailedRequest();
    }
  }

  demoUpdate(): void {
    this.dirtyForms.register('ui-kit-demo');
    this.updates.previewReady();
  }

  demoIosSheet(): void {
    this.install.showHowTo.set(true);
  }
}
