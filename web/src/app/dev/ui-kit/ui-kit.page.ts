import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ThemeService } from '../../core/theme/theme.service';
import type { AppLanguage, DensityMode, ThemeMode } from '../../core/theme/theme.types';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { CardComponent } from '../../shared/ui/card/card.component';
import { DropdownMenuComponent } from '../../shared/ui/dropdown-menu/dropdown-menu.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastHostComponent } from '../../shared/ui/toast/toast.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

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

        <kadr-card [title]="'uiKit.sampleTable' | transloco">
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-line bg-subtle text-start text-fg-2">
                  <th class="h-10 px-3 font-medium text-start">{{ 'glossary.employee' | transloco }}</th>
                  <th class="h-10 px-3 font-medium text-start">{{ 'glossary.jobTitle' | transloco }}</th>
                  <th class="h-10 px-3 font-medium text-start">{{ 'glossary.department' | transloco }}</th>
                  <th class="h-10 px-3 font-medium text-start">{{ 'glossary.pending' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                <tr class="border-b border-line">
                  <td class="h-row px-3">{{ 'uiKit.sampleName1' | transloco }}</td>
                  <td class="px-3">{{ 'uiKit.sampleRole1' | transloco }}</td>
                  <td class="px-3">{{ 'glossary.department' | transloco }}</td>
                  <td class="px-3"><kadr-badge tone="success" [label]="'ui.statusActive' | transloco" /></td>
                </tr>
                <tr class="border-b border-line">
                  <td class="h-row px-3">{{ 'uiKit.sampleName2' | transloco }}</td>
                  <td class="px-3">{{ 'uiKit.sampleRole2' | transloco }}</td>
                  <td class="px-3">{{ 'glossary.branch' | transloco }}</td>
                  <td class="px-3"><kadr-badge tone="warning" [label]="'ui.statusLeave' | transloco" /></td>
                </tr>
                <tr class="border-b border-line">
                  <td class="h-row px-3">{{ 'uiKit.sampleName3' | transloco }}</td>
                  <td class="px-3">{{ 'uiKit.sampleRole3' | transloco }}</td>
                  <td class="px-3">{{ 'glossary.attendance' | transloco }}</td>
                  <td class="px-3"><kadr-badge tone="danger" [label]="'ui.statusAbsent' | transloco" /></td>
                </tr>
              </tbody>
            </table>
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
  protected readonly swatches = SWATCHES;

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
}
