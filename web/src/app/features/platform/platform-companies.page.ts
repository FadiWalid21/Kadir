import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PlatformApi } from '../../core/api/platform.api';
import {
  PlatformTenantListItem,
  PlatformTenantSummary,
} from '../../core/api/platform.models';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { DateFmtPipe } from './date-fmt.pipe';

type PageState = 'loading' | 'ready' | 'error' | 'empty';

@Component({
  selector: 'kadr-platform-companies-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
    IconComponent,
    AvatarComponent,
    DateFmtPipe,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <header class="flex flex-col gap-2 text-start">
        <h1 class="text-2xl font-bold text-fg">{{ 'platform.companies.title' | transloco }}</h1>
        <p class="text-sm text-fg-2">
          {{
            'platform.companies.subtitleCount'
              | transloco: { count: summary()?.total ?? totalCount() }
          }}
        </p>
      </header>

      @if (summary(); as s) {
        <div class="flex gap-3 overflow-x-auto pb-1 lg:grid lg:grid-cols-4 lg:overflow-visible">
          <div class="min-w-40 shrink-0 rounded-lg border border-line bg-surface p-4 lg:min-w-0">
            <div class="flex items-center gap-2 text-fg-muted">
              <kadr-icon name="building" size="sm" />
              <p class="text-xs">{{ 'platform.companies.statTotal' | transloco }}</p>
            </div>
            <p class="mt-2 text-2xl font-bold tabular-nums text-fg">{{ s.total }}</p>
          </div>
          <div class="min-w-40 shrink-0 rounded-lg border border-line bg-surface p-4 lg:min-w-0">
            <div class="flex items-center gap-2 text-fg-muted">
              <kadr-icon name="clock" size="sm" />
              <p class="text-xs">{{ 'platform.companies.statTrial' | transloco }}</p>
            </div>
            <p class="mt-2 text-2xl font-bold tabular-nums text-fg">{{ s.trial }}</p>
          </div>
          <div class="min-w-40 shrink-0 rounded-lg border border-line bg-surface p-4 lg:min-w-0">
            <div class="flex items-center gap-2 text-fg-muted">
              <kadr-icon name="alert" size="sm" />
              <p class="text-xs">{{ 'platform.companies.statExpiring' | transloco }}</p>
            </div>
            <p class="mt-2 text-2xl font-bold tabular-nums text-fg">{{ s.expiringSoon }}</p>
          </div>
          <div class="min-w-40 shrink-0 rounded-lg border border-line bg-surface p-4 lg:min-w-0">
            <div class="flex items-center gap-2 text-fg-muted">
              <kadr-icon name="lock" size="sm" />
              <p class="text-xs">{{ 'platform.companies.statSuspended' | transloco }}</p>
            </div>
            <p class="mt-2 text-2xl font-bold tabular-nums text-fg">{{ s.suspended }}</p>
          </div>
        </div>
      }

      <div class="lg:hidden">
        <kadr-button variant="secondary" type="button" (click)="filtersOpen.set(!filtersOpen())">
          {{ 'platform.companies.filtersToggle' | transloco }}
        </kadr-button>
      </div>

      <div
        class="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4 md:flex-row md:flex-wrap md:items-end"
        [class.max-lg:hidden]="!filtersOpen()"
      >
        <label class="grid min-w-0 flex-1 gap-1 text-xs text-fg-2">
          <span>{{ 'platform.companies.search' | transloco }}</span>
          <input
            class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm text-fg"
            [ngModel]="q()"
            (ngModelChange)="q.set($event)"
            (keyup.enter)="reload()"
          />
        </label>
        <label class="grid gap-1 text-xs text-fg-2">
          <span>{{ 'platform.companies.filterStatus' | transloco }}</span>
          <select
            class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm text-fg"
            [ngModel]="status()"
            (ngModelChange)="status.set($event); page.set(1); reload()"
          >
            <option value="">{{ 'platform.companies.all' | transloco }}</option>
            <option value="Trial">{{ 'platform.status.trial' | transloco }}</option>
            <option value="Active">{{ 'platform.status.active' | transloco }}</option>
            <option value="Suspended">{{ 'platform.status.suspended' | transloco }}</option>
          </select>
        </label>
        <label class="grid gap-1 text-xs text-fg-2">
          <span>{{ 'platform.companies.filterPlan' | transloco }}</span>
          <select
            class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm text-fg"
            [ngModel]="plan()"
            (ngModelChange)="plan.set($event); page.set(1); reload()"
          >
            <option value="">{{ 'platform.companies.all' | transloco }}</option>
            <option value="starter">starter</option>
            <option value="growth">growth</option>
            <option value="business">business</option>
          </select>
        </label>
        <label class="grid gap-1 text-xs text-fg-2">
          <span>{{ 'platform.companies.filterIndustry' | transloco }}</span>
          <select
            class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm text-fg"
            [ngModel]="industry()"
            (ngModelChange)="industry.set($event); page.set(1); reload()"
          >
            <option value="">{{ 'platform.companies.all' | transloco }}</option>
            @for (code of industries; track code) {
              <option [value]="code">{{ code }}</option>
            }
          </select>
        </label>
        <kadr-button variant="secondary" type="button" (click)="clearFilters()">
          {{ 'platform.companies.clearFilters' | transloco }}
        </kadr-button>
      </div>

      @if (state() === 'loading') {
        <div class="flex flex-col gap-2" role="status" [attr.aria-label]="'ui.loading' | transloco">
          @for (_ of [1, 2, 3, 4, 5]; track $index) {
            <kadr-skeleton height="3.5rem" />
          }
        </div>
      } @else if (state() === 'error') {
        <kadr-empty-state
          [title]="'platform.companies.loadErrorTitle' | transloco"
          [description]="'platform.companies.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">
            {{ 'home.retry' | transloco }}
          </kadr-button>
        </kadr-empty-state>
      } @else if (state() === 'empty') {
        <kadr-empty-state
          [title]="'platform.companies.emptyTitle' | transloco"
          [description]="'platform.companies.emptyBody' | transloco"
        />
      } @else {
        <div class="hidden overflow-x-auto rounded-lg border border-line bg-surface md:block">
          <table class="w-full text-start text-sm">
            <thead class="border-b border-line bg-subtle text-xs text-fg-2">
              <tr>
                <th class="px-3 py-3 font-medium">{{ 'platform.companies.colCompany' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.companies.colStatus' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.companies.colPlan' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.companies.colIndustry' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.companies.colUsers' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.companies.colEmployees' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.companies.colCreated' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.companies.colTrial' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.companies.colActions' | transloco }}</th>
              </tr>
            </thead>
            <tbody>
              @for (row of items(); track row.id) {
                <tr class="border-b border-line hover:bg-hover">
                  <td class="px-3 py-3">
                    <div class="flex items-center gap-3">
                      <kadr-avatar [name]="row.name" size="sm" [ariaLabel]="row.name" />
                      <div class="min-w-0">
                        <a
                          [routerLink]="['/platform/companies', row.id]"
                          class="font-medium text-accent underline-offset-2 hover:underline"
                        >
                          {{ row.name }}
                        </a>
                        <p class="text-xs text-fg-muted"><bdi>{{ row.slug }}</bdi></p>
                      </div>
                    </div>
                  </td>
                  <td class="px-3 py-3">
                    <kadr-badge [tone]="statusTone(row.status)" [label]="statusLabel(row.status)" />
                  </td>
                  <td class="px-3 py-3 text-fg">{{ planName(row) }}</td>
                  <td class="px-3 py-3 text-fg">{{ row.industryCode }}</td>
                  <td class="px-3 py-3 tabular-nums text-fg">{{ row.userCount }}</td>
                  <td class="px-3 py-3 tabular-nums text-fg">{{ row.activeEmployeeCount }}</td>
                  <td class="px-3 py-3 tabular-nums text-fg-2">{{ row.createdAt | dateFmt }}</td>
                  <td class="px-3 py-3 text-fg-2">{{ trialLabel(row) }}</td>
                  <td class="px-3 py-3">
                    <a
                      [routerLink]="['/platform/companies', row.id]"
                      class="inline-flex size-9 items-center justify-center rounded-md text-fg-2 hover:bg-hover hover:text-fg focus-visible:outline-none"
                      [attr.aria-label]="'platform.companies.openDetail' | transloco"
                    >
                      <kadr-icon name="chevron-end" size="sm" [rtlFlip]="true" />
                    </a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <ul class="flex flex-col gap-3 md:hidden">
          @for (row of items(); track row.id) {
            <li class="rounded-lg border border-line bg-surface p-4">
              <div class="flex items-start gap-3">
                <kadr-avatar [name]="row.name" size="sm" [ariaLabel]="row.name" />
                <div class="min-w-0 flex-1">
                  <a
                    [routerLink]="['/platform/companies', row.id]"
                    class="font-medium text-accent"
                  >
                    {{ row.name }}
                  </a>
                  <p class="text-xs text-fg-muted"><bdi>{{ row.slug }}</bdi></p>
                  <div class="mt-2 flex flex-wrap items-center gap-2">
                    <kadr-badge [tone]="statusTone(row.status)" [label]="statusLabel(row.status)" />
                    <span class="text-xs text-fg-2">{{ planName(row) }}</span>
                  </div>
                  <p class="mt-2 text-xs text-fg-2">
                    {{ 'platform.companies.colUsers' | transloco }}: {{ row.userCount }} ·
                    {{ 'platform.companies.colEmployees' | transloco }}:
                    {{ row.activeEmployeeCount }}
                  </p>
                </div>
              </div>
            </li>
          }
        </ul>

        <div class="flex items-center justify-between gap-3 text-sm text-fg-2">
          <p>
            {{
              'platform.companies.pageInfo'
                | transloco: { from: pageFrom(), to: pageTo(), total: totalCount() }
            }}
          </p>
          <div class="flex gap-2">
            <kadr-button
              variant="secondary"
              type="button"
              [disabled]="page() <= 1"
              (click)="goPage(page() - 1)"
            >
              {{ 'platform.companies.prev' | transloco }}
            </kadr-button>
            <kadr-button
              variant="secondary"
              type="button"
              [disabled]="pageTo() >= totalCount()"
              (click)="goPage(page() + 1)"
            >
              {{ 'platform.companies.next' | transloco }}
            </kadr-button>
          </div>
        </div>
      }
    </div>
  `,
})
export class PlatformCompaniesPage implements OnInit {
  private readonly api = inject(PlatformApi);
  private readonly i18n = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<PlatformTenantListItem[]>([]);
  protected readonly summary = signal<PlatformTenantSummary | null>(null);
  protected readonly q = signal('');
  protected readonly status = signal('');
  protected readonly plan = signal('');
  protected readonly industry = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = 25;
  protected readonly totalCount = signal(0);
  protected readonly filtersOpen = signal(false);
  protected readonly industries = ['retail', 'clinic', 'factory', 'construction', 'office'];

  protected readonly pageFrom = computed(() =>
    this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize + 1,
  );
  protected readonly pageTo = computed(() =>
    Math.min(this.page() * this.pageSize, this.totalCount()),
  );

  ngOnInit(): void {
    void this.reload();
  }

  reload(): void {
    void this.load();
  }

  clearFilters(): void {
    this.q.set('');
    this.status.set('');
    this.plan.set('');
    this.industry.set('');
    this.page.set(1);
    void this.load();
  }

  goPage(p: number): void {
    this.page.set(p);
    void this.load();
  }

  statusTone(status: string): BadgeTone {
    switch (status) {
      case 'Active':
        return 'success';
      case 'Trial':
        return 'info';
      case 'Suspended':
        return 'warning';
      default:
        return 'neutral';
    }
  }

  statusLabel(status: string): string {
    const key = `platform.status.${status.toLowerCase()}`;
    const t = this.i18n.translate(key);
    return t === key ? status : t;
  }

  planName(row: PlatformTenantListItem): string {
    return this.i18n.getActiveLang() === 'ar' ? row.planNameAr : row.planNameEn;
  }

  trialLabel(row: PlatformTenantListItem): string {
    if (!row.trialEndsAt) {
      return '—';
    }
    try {
      return new Intl.DateTimeFormat(this.i18n.getActiveLang() === 'ar' ? 'ar' : 'en', {
        dateStyle: 'medium',
      }).format(new Date(row.trialEndsAt));
    } catch {
      return row.trialEndsAt;
    }
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      const result = await this.api.listTenants({
        q: this.q().trim() || undefined,
        status: this.status() || undefined,
        plan: this.plan() || undefined,
        industry: this.industry() || undefined,
        page: this.page(),
        pageSize: this.pageSize,
      });
      this.items.set(result.items);
      this.summary.set(result.summary);
      this.totalCount.set(result.totalCount);
      this.state.set(result.items.length === 0 ? 'empty' : 'ready');
    } catch {
      this.state.set('error');
    }
  }
}
