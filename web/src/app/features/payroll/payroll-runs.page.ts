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
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  PayrollRunListItem,
  PayrollRunsApi,
} from '../../core/api/payroll-runs.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-payroll-runs-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    SkeletonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    DrawerComponent,
    IconComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-5">
      <header class="flex flex-col gap-3 text-start sm:flex-row sm:items-end sm:justify-between">
        <div class="flex min-w-0 flex-col gap-2">
          <h1 class="text-2xl font-bold leading-tight text-fg">
            {{ 'payroll.runs.title' | transloco }}
          </h1>
          <p class="text-sm text-fg-2">{{ 'payroll.runs.subtitle' | transloco }}</p>
        </div>
        @if (canCreate()) {
          <kadr-button variant="primary" type="button" (click)="openCreate()">
            {{ 'payroll.runs.create' | transloco }}
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
          [title]="'payroll.runs.loadErrorTitle' | transloco"
          [description]="'payroll.runs.loadErrorBody' | transloco"
          (retry)="reload()"
        />
      } @else {
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'payroll.runs.kpiTotalRuns' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ totalCount() }}</p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'payroll.runs.kpiLastNet' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">
              {{ formatMoney(kpis().lastNet) }}
            </p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'payroll.runs.kpiLocked' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ kpis().locked }}</p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'payroll.runs.kpiWarnings' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ kpis().warnings }}</p>
          </div>
        </div>

        <div class="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div class="min-w-0 flex-1">
            <kadr-input
              [label]="'payroll.runs.search' | transloco"
              [value]="query()"
              (valueChange)="query.set($event)"
            />
          </div>
          <label class="flex flex-col gap-2 text-sm text-fg">
            <span class="font-medium">{{ 'payroll.runs.statusFilter' | transloco }}</span>
            <select
              class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
              [value]="statusFilter()"
              (change)="statusFilter.set($any($event.target).value)"
            >
              <option value="all">{{ 'payroll.runs.statusAll' | transloco }}</option>
              <option value="Draft">{{ 'payroll.runs.status.Draft' | transloco }}</option>
              <option value="Calculating">{{ 'payroll.runs.status.Calculating' | transloco }}</option>
              <option value="InReview">{{ 'payroll.runs.status.InReview' | transloco }}</option>
              <option value="Approved">{{ 'payroll.runs.status.Approved' | transloco }}</option>
              <option value="Locked">{{ 'payroll.runs.status.Locked' | transloco }}</option>
              <option value="Superseded">{{ 'payroll.runs.status.Superseded' | transloco }}</option>
            </select>
          </label>
        </div>

        @if (filteredItems().length === 0) {
          <kadr-empty-state
            [title]="'payroll.runs.emptyTitle' | transloco"
            [description]="'payroll.runs.emptyBody' | transloco"
          >
            @if (canCreate()) {
              <kadr-button variant="primary" type="button" (click)="openCreate()">
                {{ 'payroll.runs.create' | transloco }}
              </kadr-button>
            }
          </kadr-empty-state>
        } @else {
          <div class="hidden overflow-x-auto rounded-xl border border-line bg-surface shadow-sm md:block">
            <table class="w-full text-start text-sm">
              <thead class="bg-subtle text-xs text-fg-2">
                <tr>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colPeriod' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colStatus' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colHeadcount' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colGross' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colNet' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colCreated' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colActions' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                @for (run of filteredItems(); track run.id) {
                  <tr class="border-t border-line hover:bg-subtle/60">
                    <td class="px-3 py-3">
                      <div class="font-medium text-fg">{{ periodLabel(run) }}</div>
                      <div class="mt-0.5 text-xs tabular-nums text-fg-muted">
                        {{ formatDate(run.periodStart) }} – {{ formatDate(run.periodEnd) }}
                      </div>
                    </td>
                    <td class="px-3 py-3">
                      <span [class]="statusClass(run.status)">
                        <kadr-icon [name]="statusIcon(run.status)" size="sm" class="me-1" />
                        {{ statusLabel(run.status) }}
                      </span>
                      @if (run.warningCount > 0) {
                        <span class="ms-2 text-xs tabular-nums text-warning">
                          {{ run.warningCount }}
                        </span>
                      }
                    </td>
                    <td class="px-3 py-3 tabular-nums text-fg">{{ run.headcount }}</td>
                    <td class="px-3 py-3 tabular-nums text-fg">{{ formatMoney(run.totalGross) }}</td>
                    <td class="px-3 py-3 tabular-nums font-medium text-fg">
                      {{ formatMoney(run.totalNet) }}
                    </td>
                    <td class="px-3 py-3 tabular-nums text-fg-2">
                      {{ formatDateTime(run.createdAt) }}
                    </td>
                    <td class="px-3 py-3">
                      <a
                        [routerLink]="['/payroll/runs', run.id]"
                        class="text-sm font-medium text-accent hover:underline"
                      >
                        {{ 'payroll.runs.details' | transloco }}
                      </a>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <ul class="flex flex-col gap-3 md:hidden">
            @for (run of filteredItems(); track run.id) {
              <li class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
                <div class="flex items-start justify-between gap-2">
                  <div>
                    <p class="font-medium text-fg">{{ periodLabel(run) }}</p>
                    <p class="mt-0.5 text-xs tabular-nums text-fg-muted">
                      {{ formatDate(run.periodStart) }} – {{ formatDate(run.periodEnd) }}
                    </p>
                  </div>
                  <span [class]="statusClass(run.status)">{{ statusLabel(run.status) }}</span>
                </div>
                <dl class="mt-3 grid grid-cols-2 gap-2 text-xs">
                  <div>
                    <dt class="text-fg-2">{{ 'payroll.runs.colHeadcount' | transloco }}</dt>
                    <dd class="mt-0.5 tabular-nums font-medium text-fg">{{ run.headcount }}</dd>
                  </div>
                  <div>
                    <dt class="text-fg-2">{{ 'payroll.runs.colNet' | transloco }}</dt>
                    <dd class="mt-0.5 tabular-nums font-medium text-fg">
                      {{ formatMoney(run.totalNet) }}
                    </dd>
                  </div>
                </dl>
                <a
                  [routerLink]="['/payroll/runs', run.id]"
                  class="mt-3 inline-flex text-sm font-medium text-accent hover:underline"
                >
                  {{ 'payroll.runs.details' | transloco }}
                </a>
              </li>
            }
          </ul>

          <div class="flex flex-wrap items-center justify-between gap-2 text-sm text-fg-2">
            <p>
              {{
                'payroll.runs.showing'
                  | transloco: { n: filteredItems().length, total: totalCount() }
              }}
            </p>
            <div class="flex gap-2">
              <kadr-button
                variant="secondary"
                type="button"
                [disabled]="page() <= 1"
                (click)="prevPage()"
              >
                {{ 'payroll.runs.prev' | transloco }}
              </kadr-button>
              <kadr-button
                variant="secondary"
                type="button"
                [disabled]="page() * pageSize >= totalCount()"
                (click)="nextPage()"
              >
                {{ 'payroll.runs.next' | transloco }}
              </kadr-button>
            </div>
          </div>
        }
      }
    </div>

    <kadr-drawer
      [open]="createOpen()"
      [title]="'payroll.runs.createTitle' | transloco"
      [subtitle]="'payroll.runs.createSubtitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="createOpen.set(false)"
    >
      <form class="flex flex-col gap-4 text-start" (ngSubmit)="createRun()">
        <kadr-input
          [label]="'payroll.runs.periodStart' | transloco"
          [value]="periodStart()"
          (valueChange)="periodStart.set($event)"
          type="date"
        />
        <kadr-input
          [label]="'payroll.runs.periodEnd' | transloco"
          [value]="periodEnd()"
          (valueChange)="periodEnd.set($event)"
          type="date"
        />
        <p class="text-xs text-fg-muted">{{ 'payroll.runs.createHint' | transloco }}</p>
        <div drawerFooter class="flex w-full flex-wrap gap-2">
          <kadr-button variant="primary" type="submit" [loading]="busy()">
            {{ 'payroll.runs.createSubmit' | transloco }}
          </kadr-button>
          <kadr-button variant="ghost" type="button" (click)="createOpen.set(false)">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
        </div>
      </form>
    </kadr-drawer>
  `,
})
export class PayrollRunsPage implements OnInit {
  private readonly api = inject(PayrollRunsApi);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly title = inject(Title);
  private readonly router = inject(Router);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<PayrollRunListItem[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = 20;
  protected readonly query = signal('');
  protected readonly statusFilter = signal('all');
  protected readonly busy = signal(false);
  protected readonly createOpen = signal(false);
  protected readonly periodStart = signal('');
  protected readonly periodEnd = signal('');

  protected readonly canCreate = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.PayrollRun) && !this.subscription.readOnly(),
  );

  protected readonly filteredItems = computed(() => {
    const q = this.query().trim().toLowerCase();
    const status = this.statusFilter();
    return this.items().filter((run) => {
      if (status !== 'all' && run.status !== status) return false;
      if (!q) return true;
      const label = this.periodLabel(run).toLowerCase();
      return (
        label.includes(q) ||
        run.periodStart.includes(q) ||
        run.periodEnd.includes(q) ||
        run.status.toLowerCase().includes(q)
      );
    });
  });

  protected readonly kpis = computed(() => {
    const list = this.items();
    const withNet = list.find((x) => x.totalNet != null);
    return {
      lastNet: withNet?.totalNet ?? null,
      locked: list.filter((x) => x.status === 'Locked').length,
      warnings: list.reduce((sum, x) => sum + x.warningCount, 0),
    };
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('payroll.runs.title'));
    void this.reload();
  }

  async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const page = await this.api.list({ page: this.page(), pageSize: this.pageSize });
      this.items.set(page.items);
      this.totalCount.set(page.totalCount);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  prevPage(): void {
    if (this.page() <= 1) return;
    this.page.update((p) => p - 1);
    void this.reload();
  }

  nextPage(): void {
    if (this.page() * this.pageSize >= this.totalCount()) return;
    this.page.update((p) => p + 1);
    void this.reload();
  }

  openCreate(): void {
    const now = new Date();
    const y = now.getUTCFullYear();
    const m = now.getUTCMonth();
    const start = new Date(Date.UTC(y, m, 1));
    const end = new Date(Date.UTC(y, m + 1, 0));
    this.periodStart.set(start.toISOString().slice(0, 10));
    this.periodEnd.set(end.toISOString().slice(0, 10));
    this.createOpen.set(true);
  }

  async createRun(): Promise<void> {
    const periodStart = this.periodStart().trim();
    const periodEnd = this.periodEnd().trim();
    if (!periodStart || !periodEnd || periodEnd < periodStart) {
      this.toast.show(this.transloco.translate('payroll.runs.toastValidation'), 'danger');
      return;
    }
    this.busy.set(true);
    try {
      const created = await this.api.create({ periodStart, periodEnd });
      this.createOpen.set(false);
      this.toast.show(this.transloco.translate('payroll.runs.toastCreated'), 'success');
      await this.router.navigate(['/payroll/runs', created.id]);
    } catch (err) {
      const code = problemDetailsFromError(err)?.code;
      const key =
        code === 'payroll.rules.not_confirmed'
          ? 'payroll.runs.toastRulesNotConfirmed'
          : 'payroll.runs.toastFail';
      this.toast.show(this.transloco.translate(key), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  periodLabel(run: PayrollRunListItem): string {
    const d = new Date(`${run.periodEnd}T00:00:00Z`);
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-GB';
    return new Intl.DateTimeFormat(lang, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(
      d,
    );
  }

  statusLabel(status: string): string {
    const key = `payroll.runs.status.${status}`;
    const t = this.transloco.translate(key);
    return t === key ? status : t;
  }

  statusIcon(status: string): 'clock' | 'check' | 'lock' | 'alert' | 'file' | 'refresh' {
    if (status === 'Locked') return 'lock';
    if (status === 'Approved') return 'check';
    if (status === 'InReview' || status === 'Calculating') return 'clock';
    if (status === 'Superseded') return 'refresh';
    if (status === 'Draft') return 'file';
    return 'alert';
  }

  statusClass(status: string): string {
    const base =
      'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ';
    if (status === 'Locked') return base + 'bg-success-soft text-success';
    if (status === 'Approved') return base + 'bg-info-soft text-info';
    if (status === 'InReview' || status === 'Calculating') {
      return base + 'bg-warning-soft text-fg';
    }
    if (status === 'Superseded') return base + 'bg-danger-soft text-danger';
    return base + 'bg-subtle text-fg-2';
  }

  formatMoney(value: number | null | undefined): string {
    if (value == null) return '—';
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-EG';
    return new Intl.NumberFormat(lang, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }

  formatDate(iso: string): string {
    const d = new Date(`${iso}T00:00:00Z`);
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-GB';
    return new Intl.DateTimeFormat(lang, { dateStyle: 'medium', timeZone: 'UTC' }).format(d);
  }

  formatDateTime(iso: string): string {
    const d = new Date(iso);
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-GB';
    return new Intl.DateTimeFormat(lang, { dateStyle: 'medium', timeStyle: 'short' }).format(d);
  }
}
