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
import {
  AttendanceApi,
  AttendanceMatrix,
  BranchLocation,
} from '../../core/api/attendance.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-attendance-reports-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
  ],
  template: `

    <header class="mb-4 flex flex-wrap items-start justify-between gap-3 text-start">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.reports.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.reports.subtitle' | transloco }}</p>
      </div>
      <kadr-button variant="secondary" type="button" [disabled]="!matrix()?.items.length" (click)="exportCsv()">
        {{ 'ui.export' | transloco }}
      </kadr-button>
    </header>

    <div class="mb-4 flex flex-wrap items-center gap-2">
      <kadr-button variant="ghost" type="button" (click)="shiftMonth(-1)">‹</kadr-button>
      <p class="min-w-32 text-center text-sm font-medium tabular-nums text-fg">{{ monthLabel() }}</p>
      <kadr-button variant="ghost" type="button" (click)="shiftMonth(1)">›</kadr-button>
      <select
        class="rounded-md border border-line bg-surface px-3 py-2 text-sm text-fg"
        [(ngModel)]="branchFilter"
        (ngModelChange)="reload()"
      >
        <option value="">{{ 'attendance.today.allBranches' | transloco }}</option>
        @for (br of branches(); track br.branchId) {
          <option [value]="br.branchId">{{ br.name }}</option>
        }
      </select>
      <input
        type="search"
        class="min-w-48 flex-1 rounded-md border border-line bg-surface px-3 py-2 text-sm text-fg"
        [placeholder]="'attendance.today.search' | transloco"
        [(ngModel)]="query"
        (keyup.enter)="reload()"
      />
      <kadr-button variant="secondary" type="button" (click)="reload()">
        {{ 'attendance.today.applySearch' | transloco }}
      </kadr-button>
    </div>

    @if (state() === 'loading') {
      <kadr-skeleton height="20rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.reports.loadErrorTitle' | transloco"
        [description]="'attendance.reports.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (matrix(); as m) {
      <div class="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.reports.commitment' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ m.commitmentPercent }}%</p>
        </article>
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.reports.totalLate' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-warning">{{ m.totalLateMinutes }}</p>
        </article>
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.reports.totalOt' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-info">{{ m.totalOvertimeMinutes }}</p>
        </article>
      </div>

      <div class="mb-3 flex flex-wrap gap-2 text-xs text-fg-2">
        <span class="inline-flex items-center gap-1">
          <span class="size-2.5 rounded-sm bg-success"></span>
          {{ 'attendance.day.Present' | transloco }}
        </span>
        <span class="inline-flex items-center gap-1">
          <span class="size-2.5 rounded-sm bg-warning"></span>
          {{ 'attendance.day.Late' | transloco }}
        </span>
        <span class="inline-flex items-center gap-1">
          <span class="size-2.5 rounded-sm bg-danger"></span>
          {{ 'attendance.day.Absent' | transloco }}
        </span>
        <span class="inline-flex items-center gap-1">
          <span class="size-2.5 rounded-sm bg-line-strong"></span>
          {{ 'attendance.day.Weekend' | transloco }} / {{ 'attendance.day.Holiday' | transloco }}
        </span>
      </div>

      @if (m.items.length === 0) {
        <kadr-empty-state
          [title]="'attendance.reports.emptyTitle' | transloco"
          [description]="'attendance.reports.emptyBody' | transloco"
        />
      } @else {
        <div class="overflow-x-auto rounded-lg border border-line bg-surface shadow-sm">
          <table class="w-full min-w-[48rem] text-start text-xs">
            <thead class="border-b border-line bg-app text-fg-2">
              <tr>
                <th class="sticky start-0 bg-app px-3 py-2 font-medium">
                  {{ 'attendance.today.colEmployee' | transloco }}
                </th>
                @for (d of dayHeaders(); track d) {
                  <th class="px-1 py-2 text-center font-medium tabular-nums">{{ d.slice(-2) }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of m.items; track row.employeeId) {
                <tr class="border-b border-line last:border-0">
                  <td class="sticky start-0 bg-surface px-3 py-2">
                    <p class="text-sm font-medium text-fg">
                      {{ displayName(row.fullNameAr, row.fullNameEn) }}
                    </p>
                    <p class="tabular-nums text-fg-muted">{{ row.presentDays }}</p>
                  </td>
                  @for (day of row.days; track day.date) {
                    <td class="px-0.5 py-1 text-center">
                      <span
                        class="inline-block size-5 rounded-sm"
                        [class]="cellClass(day.status)"
                        [attr.title]="statusLabel(day.status)"
                      ></span>
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="mt-4 flex items-center justify-between gap-2 text-sm text-fg-2">
          <kadr-button variant="secondary" type="button" [disabled]="page() <= 1" (click)="setPage(page() - 1)">
            {{ 'attendance.today.prev' | transloco }}
          </kadr-button>
          <span class="tabular-nums">{{ page() }} / {{ totalPages() }}</span>
          <kadr-button
            variant="secondary"
            type="button"
            [disabled]="page() >= totalPages()"
            (click)="setPage(page() + 1)"
          >
            {{ 'attendance.today.next' | transloco }}
          </kadr-button>
        </div>
      }
    }
  `,
})
export class AttendanceReportsPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly matrix = signal<AttendanceMatrix | null>(null);
  protected readonly branches = signal<BranchLocation[]>([]);
  protected readonly year = signal(new Date().getFullYear());
  protected readonly month = signal(new Date().getMonth() + 1);
  protected readonly page = signal(1);
  protected branchFilter = '';
  protected query = '';

  protected readonly monthLabel = computed(() => {
    const d = new Date(this.year(), this.month() - 1, 1);
    return d.toLocaleDateString(this.transloco.getActiveLang() === 'en' ? 'en' : 'ar', {
      month: 'long',
      year: 'numeric',
    });
  });

  protected readonly dayHeaders = computed(() => this.matrix()?.items[0]?.days.map((d) => d.date) ?? []);

  protected readonly totalPages = computed(() => {
    const m = this.matrix();
    if (!m || m.pageSize <= 0) return 1;
    return Math.max(1, Math.ceil(m.totalCount / m.pageSize));
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.reports.title'));
    void this.api.listBranches().then((b) => this.branches.set(b)).catch(() => undefined);
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const data = await this.api.getMatrix({
        year: this.year(),
        month: this.month(),
        branchId: this.branchFilter || undefined,
        q: this.query.trim() || undefined,
        page: this.page(),
        pageSize: 20,
      });
      this.matrix.set(data);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected shiftMonth(delta: number): void {
    let y = this.year();
    let m = this.month() + delta;
    if (m < 1) {
      m = 12;
      y -= 1;
    } else if (m > 12) {
      m = 1;
      y += 1;
    }
    this.year.set(y);
    this.month.set(m);
    this.page.set(1);
    void this.reload();
  }

  protected setPage(p: number): void {
    this.page.set(p);
    void this.reload();
  }

  protected displayName(ar: string, en: string): string {
    return this.transloco.getActiveLang() === 'en' ? en || ar : ar || en;
  }

  protected statusLabel(status: string): string {
    const key = `attendance.day.${status}`;
    const t = this.transloco.translate(key);
    return t === key ? status : t;
  }

  protected cellClass(status: string): string {
    switch (status) {
      case 'Present':
        return 'bg-success';
      case 'Late':
      case 'Incomplete':
        return 'bg-warning';
      case 'Absent':
        return 'bg-danger';
      case 'Leave':
        return 'bg-info';
      case 'Weekend':
      case 'Holiday':
      case 'NoShift':
        return 'bg-line-strong';
      default:
        return 'bg-app border border-line';
    }
  }

  protected exportCsv(): void {
    const m = this.matrix();
    if (!m) return;
    const headers = ['employee', 'number', ...this.dayHeaders()];
    const lines = [headers.join(',')];
    for (const row of m.items) {
      const cells = [
        `"${(this.displayName(row.fullNameAr, row.fullNameEn) || '').replace(/"/g, '""')}"`,
        row.employeeNumber,
        ...row.days.map((d) => d.status),
      ];
      lines.push(cells.join(','));
    }
    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `attendance-matrix-${m.year}-${String(m.month).padStart(2, '0')}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }
}
