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
  AttendanceBoard,
  AttendanceBoardRow,
  BranchLocation,
} from '../../core/api/attendance.api';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-attendance-today-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    BadgeComponent,
    ButtonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
  ],
  template: `

    <header class="mb-4 flex flex-wrap items-start justify-between gap-3 text-start">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.today.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ dateLabel() }}</p>
      </div>
      <div class="flex items-center gap-2">
        <kadr-button variant="ghost" type="button" (click)="shiftDate(-1)">‹</kadr-button>
        <input
          type="date"
          class="rounded-md border border-line bg-surface px-3 py-2 text-sm text-fg"
          [value]="dateIso()"
          (change)="onDateInput($any($event.target).value)"
        />
        <kadr-button variant="ghost" type="button" (click)="shiftDate(1)">›</kadr-button>
      </div>
    </header>

    @if (state() === 'loading') {
      <div class="flex flex-col gap-3">
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-5">
          @for (_ of [1, 2, 3, 4, 5]; track $index) {
            <kadr-skeleton height="5rem" />
          }
        </div>
        <kadr-skeleton height="16rem" />
      </div>
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.today.loadErrorTitle' | transloco"
        [description]="'attendance.today.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (board(); as b) {
      @if (b.dayKind === 'Holiday' || b.dayKind === 'Weekend') {
        <div class="mb-4 rounded-lg border border-line bg-info-soft px-4 py-3 text-sm text-fg" role="status">
          {{ 'attendance.today.nonWorkday' | transloco }}
        </div>
      }

      <div class="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-5">
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.today.statPresent' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-success">{{ b.stats.present }}</p>
        </article>
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.today.statLate' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-warning">{{ b.stats.late }}</p>
        </article>
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.today.statAbsent' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-danger">{{ b.stats.absent }}</p>
        </article>
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.today.statLeave' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-info">{{ b.stats.onLeave }}</p>
        </article>
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.today.statNotYet' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ b.stats.notYet }}</p>
        </article>
      </div>

      <div class="mb-4 flex flex-wrap gap-2">
        <select
          class="rounded-md border border-line bg-surface px-3 py-2 text-sm text-fg"
          [(ngModel)]="statusFilter"
          (ngModelChange)="reload()"
        >
          <option value="">{{ 'attendance.today.filterAll' | transloco }}</option>
          <option value="present">{{ 'attendance.today.statPresent' | transloco }}</option>
          <option value="late">{{ 'attendance.today.statLate' | transloco }}</option>
          <option value="absent">{{ 'attendance.today.statAbsent' | transloco }}</option>
          <option value="notyet">{{ 'attendance.today.statNotYet' | transloco }}</option>
        </select>
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
        <kadr-button
          variant="secondary"
          type="button"
          [disabled]="true"
          [attr.title]="'attendance.today.exportNotWired' | transloco"
        >
          {{ 'ui.export' | transloco }}
        </kadr-button>
      </div>

      @if (b.items.length === 0) {
        <kadr-empty-state
          [title]="'attendance.today.emptyTitle' | transloco"
          [description]="'attendance.today.emptyBody' | transloco"
        />
      } @else {
        <div class="hidden overflow-x-auto rounded-lg border border-line bg-surface shadow-sm md:block">
          <table class="w-full min-w-[48rem] text-start text-sm">
            <thead class="border-b border-line bg-app text-fg-2">
              <tr>
                <th class="px-4 py-3 font-medium">{{ 'attendance.today.colEmployee' | transloco }}</th>
                <th class="px-4 py-3 font-medium">{{ 'attendance.today.colIn' | transloco }}</th>
                <th class="px-4 py-3 font-medium">{{ 'attendance.today.colElapsed' | transloco }}</th>
                <th class="px-4 py-3 font-medium">{{ 'attendance.today.colGeo' | transloco }}</th>
                <th class="px-4 py-3 font-medium">{{ 'attendance.today.colShift' | transloco }}</th>
                <th class="px-4 py-3 font-medium">{{ 'attendance.today.colBranch' | transloco }}</th>
                <th class="px-4 py-3 font-medium">{{ 'attendance.today.colStatus' | transloco }}</th>
              </tr>
            </thead>
            <tbody>
              @for (row of b.items; track row.employeeId) {
                <tr class="border-b border-line last:border-0">
                  <td class="px-4 py-3">
                    <div class="flex items-center gap-3">
                      <span
                        class="inline-flex size-9 shrink-0 items-center justify-center rounded-full bg-accent-soft text-xs font-medium text-accent-soft-fg"
                        aria-hidden="true"
                      >
                        {{ initials(row) }}
                      </span>
                      <div class="min-w-0">
                        <p class="truncate font-medium text-fg">{{ displayName(row) }}</p>
                        <p class="text-xs tabular-nums text-fg-muted">
                          <bdi>{{ row.employeeNumber }}</bdi>
                        </p>
                      </div>
                    </div>
                  </td>
                  <td class="px-4 py-3 tabular-nums text-fg-2">
                    {{ row.firstInAt ? formatTime(row.firstInAt) : '—' }}
                  </td>
                  <td class="px-4 py-3 tabular-nums text-fg-2">{{ elapsedLabel(row) }}</td>
                  <td class="px-4 py-3">
                    @if (row.firstInAt) {
                      <kadr-badge
                        [tone]="row.isInsideGeofence ? 'success' : 'warning'"
                        [label]="
                          (row.isInsideGeofence
                            ? 'attendance.today.inside'
                            : 'attendance.today.outside'
                          ) | transloco
                        "
                      />
                    } @else {
                      <span class="text-fg-muted">—</span>
                    }
                  </td>
                  <td class="px-4 py-3 text-fg-2">{{ shiftLabel(row) }}</td>
                  <td class="px-4 py-3 text-fg-2">{{ row.branchName }}</td>
                  <td class="px-4 py-3">
                    <kadr-badge [tone]="statusTone(row.status)" [label]="statusLabel(row.status)" />
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <ul class="flex flex-col gap-2 md:hidden">
          @for (row of b.items; track row.employeeId) {
            <li class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
              <div class="flex items-start justify-between gap-2">
                <div>
                  <p class="text-sm font-medium text-fg">{{ displayName(row) }}</p>
                  <p class="text-xs text-fg-muted">{{ row.branchName }}</p>
                </div>
                <kadr-badge [tone]="statusTone(row.status)" [label]="statusLabel(row.status)" />
              </div>
              <p class="mt-2 text-xs tabular-nums text-fg-2">
                {{ 'attendance.today.colIn' | transloco }}:
                {{ row.firstInAt ? formatTime(row.firstInAt) : '—' }}
              </p>
            </li>
          }
        </ul>

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
export class AttendanceTodayPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly board = signal<AttendanceBoard | null>(null);
  protected readonly branches = signal<BranchLocation[]>([]);
  protected readonly dateIso = signal(new Date().toISOString().slice(0, 10));
  protected readonly page = signal(1);
  protected statusFilter = '';
  protected branchFilter = '';
  protected query = '';

  protected readonly dateLabel = computed(() => {
    const [y, m, d] = this.dateIso().split('-').map(Number);
    return new Date(y, m - 1, d).toLocaleDateString(
      this.transloco.getActiveLang() === 'en' ? 'en' : 'ar',
      { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' },
    );
  });

  protected readonly totalPages = computed(() => {
    const b = this.board();
    if (!b || b.pageSize <= 0) return 1;
    return Math.max(1, Math.ceil(b.totalCount / b.pageSize));
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.today.title'));
    void this.api.listBranches().then((b) => this.branches.set(b)).catch(() => undefined);
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const data = await this.api.getBoardToday({
        date: this.dateIso(),
        branchId: this.branchFilter || undefined,
        status: this.statusFilter || undefined,
        q: this.query.trim() || undefined,
        page: this.page(),
        pageSize: 20,
      });
      this.board.set(data);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected shiftDate(delta: number): void {
    const [y, m, d] = this.dateIso().split('-').map(Number);
    const dt = new Date(y, m - 1, d + delta);
    this.dateIso.set(
      `${dt.getFullYear()}-${String(dt.getMonth() + 1).padStart(2, '0')}-${String(dt.getDate()).padStart(2, '0')}`,
    );
    this.page.set(1);
    void this.reload();
  }

  protected onDateInput(value: string): void {
    if (!value) return;
    this.dateIso.set(value);
    this.page.set(1);
    void this.reload();
  }

  protected setPage(p: number): void {
    this.page.set(p);
    void this.reload();
  }

  protected displayName(row: AttendanceBoardRow): string {
    return this.transloco.getActiveLang() === 'en'
      ? row.fullNameEn || row.fullNameAr
      : row.fullNameAr || row.fullNameEn;
  }

  protected initials(row: AttendanceBoardRow): string {
    const name = this.displayName(row).trim();
    const parts = name.split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return '?';
    }
    if (parts.length === 1) {
      return parts[0].slice(0, 2).toUpperCase();
    }
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }

  protected shiftLabel(row: AttendanceBoardRow): string {
    const name =
      this.transloco.getActiveLang() === 'en'
        ? row.shiftNameEn || row.shiftNameAr
        : row.shiftNameAr || row.shiftNameEn;
    if (!name) return '—';
    const start = row.shiftStart?.slice(0, 5) ?? '';
    const end = row.shiftEnd?.slice(0, 5) ?? '';
    return start && end ? `${name} (${start}–${end})` : name;
  }

  protected statusLabel(status: string): string {
    if (status === 'NotYet') return this.transloco.translate('attendance.today.statNotYet');
    const key = `attendance.day.${status}`;
    const t = this.transloco.translate(key);
    return t === key ? status : t;
  }

  protected statusTone(status: string): BadgeTone {
    switch (status) {
      case 'Present':
        return 'success';
      case 'Late':
      case 'Incomplete':
      case 'NotYet':
        return 'warning';
      case 'Absent':
        return 'danger';
      case 'Leave':
      case 'Holiday':
        return 'info';
      default:
        return 'neutral';
    }
  }

  protected formatTime(iso: string): string {
    return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  }

  protected elapsedLabel(row: AttendanceBoardRow): string {
    if (row.workedMinutes > 0) {
      const h = Math.floor(row.workedMinutes / 60);
      const m = row.workedMinutes % 60;
      return `${h}:${String(m).padStart(2, '0')}`;
    }
    if (row.firstInAt && !row.lastOutAt) {
      const mins = Math.max(0, Math.floor((Date.now() - new Date(row.firstInAt).getTime()) / 60000));
      const h = Math.floor(mins / 60);
      const m = mins % 60;
      return `${h}:${String(m).padStart(2, '0')}`;
    }
    return '—';
  }
}
