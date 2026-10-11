import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AttendanceApi, AttendanceMonth, AttendanceMonthDay } from '../../core/api/attendance.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { AttendanceSubnavComponent } from './attendance-subnav.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-my-month-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    AttendanceSubnavComponent,
    ButtonComponent,
    DrawerComponent,
    EmptyStateComponent,
    IconComponent,
    NetworkErrorComponent,
    SkeletonComponent,
  ],
  template: `
    <kadr-attendance-subnav active="me" />

    <header class="mb-6 flex flex-wrap items-center justify-between gap-3 text-start">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.myMonth.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.myMonth.subtitle' | transloco }}</p>
      </div>
      <div class="flex items-center gap-2">
        <kadr-button variant="ghost" type="button" [attr.aria-label]="'attendance.myMonth.prev' | transloco" (click)="shiftMonth(-1)">
          <kadr-icon name="chevrons" [rtlFlip]="true" size="sm" />
        </kadr-button>
        <p class="min-w-28 text-center text-sm font-medium tabular-nums text-fg">{{ monthLabel() }}</p>
        <kadr-button variant="ghost" type="button" [attr.aria-label]="'attendance.myMonth.next' | transloco" (click)="shiftMonth(1)">
          <kadr-icon name="chevrons" size="sm" />
        </kadr-button>
      </div>
    </header>

    @if (state() === 'loading') {
      <div class="flex flex-col gap-4" role="status" [attr.aria-label]="'ui.loading' | transloco">
        <div class="grid grid-cols-2 gap-3 md:grid-cols-4">
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
        </div>
        <kadr-skeleton height="16rem" />
      </div>
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.myMonth.loadErrorTitle' | transloco"
        [description]="'attendance.myMonth.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (month(); as m) {
      <div class="mb-6 grid grid-cols-2 gap-3 md:grid-cols-4">
        <div class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.myMonth.present' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ m.presentDays + m.lateDays }}</p>
        </div>
        <div class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'glossary.late' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ m.lateDays }}</p>
        </div>
        <div class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'glossary.absent' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ m.absentDays }}</p>
        </div>
        <div class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.myMonth.workedHours' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ hoursLabel(m.totalWorkedMinutes) }}</p>
        </div>
      </div>

      <div class="mb-4 flex justify-end">
        <kadr-button variant="secondary" type="button" (click)="listMode.set(!listMode())">
          {{ (listMode() ? 'attendance.myMonth.showCalendar' : 'attendance.myMonth.showList') | transloco }}
        </kadr-button>
      </div>

      @if (m.days.length === 0) {
        <kadr-empty-state
          [title]="'attendance.myMonth.emptyTitle' | transloco"
          [description]="'attendance.myMonth.emptyBody' | transloco"
        />
      } @else if (listMode()) {
        <ul class="flex flex-col gap-2">
          @for (day of m.days; track day.date) {
            <li>
              <button
                type="button"
                class="flex w-full items-center justify-between gap-3 rounded-lg border border-line bg-surface px-4 py-3 text-start shadow-sm hover:bg-hover"
                (click)="openDay(day)"
              >
                <span class="text-sm font-medium tabular-nums text-fg">{{ day.date }}</span>
                <span class="inline-flex items-center gap-2 text-sm text-fg-2">
                  <kadr-icon [name]="statusIcon(day.status)" size="sm" />
                  {{ statusLabel(day.status) }}
                </span>
              </button>
            </li>
          }
        </ul>
      } @else {
        <div class="grid grid-cols-7 gap-1 rounded-lg border border-line bg-surface p-3 shadow-sm">
          @for (day of calendarCells(); track $index) {
            @if (day) {
              <button
                type="button"
                class="flex min-h-16 flex-col items-center justify-center gap-1 rounded-md p-1 text-xs hover:bg-hover"
                (click)="openDay(day)"
              >
                <span class="tabular-nums text-fg">{{ day.date.slice(-2) }}</span>
                <span class="inline-flex items-center gap-1 text-fg-2">
                  <kadr-icon [name]="statusIcon(day.status)" size="sm" />
                </span>
              </button>
            } @else {
              <div class="min-h-16"></div>
            }
          }
        </div>
      }
    }

    <kadr-drawer
      [open]="!!selected()"
      [title]="selected()?.date ?? ''"
      [closeLabel]="'ui.close' | transloco"
      (closed)="selected.set(null)"
    >
      @if (selected(); as day) {
        <div class="flex flex-col gap-3 p-4 text-start">
          <p class="inline-flex items-center gap-2 text-sm text-fg">
            <kadr-icon [name]="statusIcon(day.status)" size="sm" />
            {{ statusLabel(day.status) }}
          </p>
          <p class="text-sm text-fg-2">
            {{ 'attendance.myMonth.firstIn' | transloco }}:
            {{ day.firstInAt ? formatTime(day.firstInAt) : '—' }}
          </p>
          <p class="text-sm text-fg-2">
            {{ 'attendance.myMonth.lastOut' | transloco }}:
            {{ day.lastOutAt ? formatTime(day.lastOutAt) : '—' }}
          </p>
          <p class="text-sm text-fg-2">
            {{ 'attendance.myMonth.worked' | transloco }}: {{ hoursLabel(day.workedMinutes) }}
          </p>
          <p class="text-sm text-fg-muted">{{ 'attendance.myMonth.correctionHint' | transloco }}</p>
        </div>
      }
    </kadr-drawer>
  `,
})
export class MyMonthPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly month = signal<AttendanceMonth | null>(null);
  protected readonly year = signal(new Date().getFullYear());
  protected readonly monthNum = signal(new Date().getMonth() + 1);
  protected readonly listMode = signal(false);
  protected readonly selected = signal<AttendanceMonthDay | null>(null);

  protected readonly monthLabel = computed(() => {
    const d = new Date(this.year(), this.monthNum() - 1, 1);
    return d.toLocaleDateString(this.transloco.getActiveLang() === 'en' ? 'en' : 'ar', {
      month: 'long',
      year: 'numeric',
    });
  });

  protected readonly calendarCells = computed(() => {
    const m = this.month();
    if (!m) {
      return [];
    }
    const first = new Date(m.year, m.month - 1, 1);
    // Sunday-start grid to match EG work week start
    const pad = first.getDay();
    const byDate = new Map(m.days.map((d) => [d.date, d]));
    const cells: (AttendanceMonthDay | null)[] = [];
    for (let i = 0; i < pad; i++) {
      cells.push(null);
    }
    const daysInMonth = new Date(m.year, m.month, 0).getDate();
    for (let day = 1; day <= daysInMonth; day++) {
      const key = `${m.year}-${String(m.month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
      cells.push(byDate.get(key) ?? null);
    }
    return cells;
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.myMonth.title'));
    void this.reload();
  }

  protected shiftMonth(delta: number): void {
    let y = this.year();
    let m = this.monthNum() + delta;
    if (m < 1) {
      m = 12;
      y -= 1;
    } else if (m > 12) {
      m = 1;
      y += 1;
    }
    this.year.set(y);
    this.monthNum.set(m);
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const data = await this.api.getMyMonth(this.year(), this.monthNum());
      this.month.set(data);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected openDay(day: AttendanceMonthDay): void {
    this.selected.set(day);
  }

  protected hoursLabel(minutes: number): string {
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return `${h}:${String(m).padStart(2, '0')}`;
  }

  protected statusLabel(status: string): string {
    const key = `attendance.day.${status}`;
    const translated = this.transloco.translate(key);
    return translated === key ? status : translated;
  }

  protected statusIcon(status: string): 'check' | 'clock' | 'inbox' | 'calendar' {
    switch (status) {
      case 'Present':
        return 'check';
      case 'Late':
      case 'Incomplete':
        return 'clock';
      case 'Absent':
        return 'inbox';
      default:
        return 'calendar';
    }
  }

  protected formatTime(iso: string): string {
    return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  }
}
