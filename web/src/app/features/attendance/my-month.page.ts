import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AttendanceApi, AttendanceMonth, AttendanceMonthDay } from '../../core/api/attendance.api';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { CorrectionRequestDrawerComponent } from './correction-request-drawer.component';

type PageState = 'loading' | 'ready' | 'error' | 'noEmployee';
type ViewMode = 'list' | 'calendar';

@Component({
  selector: 'kadr-my-month-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgTemplateOutlet,
    TranslocoPipe,
    BadgeComponent,
    ButtonComponent,
    DrawerComponent,
    EmptyStateComponent,
    IconComponent,
    NetworkErrorComponent,
    SkeletonComponent,
    CorrectionRequestDrawerComponent,
  ],
  template: `

    <header class="mb-6 flex flex-wrap items-center justify-between gap-3 text-start">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.myMonth.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.myMonth.subtitle' | transloco }}</p>
      </div>
      <div class="flex items-center gap-2">
        <kadr-button
          variant="ghost"
          type="button"
          [attr.aria-label]="'attendance.myMonth.prev' | transloco"
          (click)="shiftMonth(-1)"
        >
          <kadr-icon name="chevrons" [rtlFlip]="true" size="sm" />
        </kadr-button>
        <p class="min-w-32 text-center text-sm font-medium tabular-nums text-fg">{{ monthLabel() }}</p>
        <kadr-button
          variant="ghost"
          type="button"
          [attr.aria-label]="'attendance.myMonth.next' | transloco"
          (click)="shiftMonth(1)"
        >
          <kadr-icon name="chevrons" size="sm" />
        </kadr-button>
      </div>
    </header>

    @if (state() === 'loading') {
      <div class="flex flex-col gap-4" role="status" [attr.aria-label]="'ui.loading' | transloco">
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <kadr-skeleton height="5.5rem" />
          <kadr-skeleton height="5.5rem" />
          <kadr-skeleton height="5.5rem" />
          <kadr-skeleton height="5.5rem" />
        </div>
        <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
          <kadr-skeleton height="18rem" />
          <kadr-skeleton height="18rem" />
        </div>
      </div>
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.myMonth.loadErrorTitle' | transloco"
        [description]="'attendance.myMonth.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (state() === 'noEmployee') {
      <kadr-empty-state
        [title]="'attendance.myMonth.noEmployeeTitle' | transloco"
        [description]="'attendance.myMonth.noEmployeeBody' | transloco"
      />
    } @else if (month(); as m) {
      <div class="mb-6 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.myMonth.present' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-fg">
            {{ m.presentDays + m.lateDays }}
            <span class="text-sm font-medium text-fg-2">/ {{ workdayCount() }}</span>
          </p>
        </article>
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'glossary.late' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-warning">
            {{ m.totalLateMinutes }}
            <span class="text-sm font-medium">{{ 'attendance.myMonth.minutesUnit' | transloco }}</span>
          </p>
        </article>
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'glossary.absent' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-danger">{{ m.absentDays }}</p>
        </article>
        <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
          <p class="text-xs text-fg-muted">{{ 'attendance.myMonth.overtime' | transloco }}</p>
          <p class="mt-1 text-xl font-bold tabular-nums text-fg">
            {{ hoursLabel(m.totalOvertimeMinutes) }}
          </p>
          <p class="mt-1 text-xs text-fg-muted">
            {{ 'attendance.myMonth.workedHours' | transloco }}: {{ hoursLabel(m.totalWorkedMinutes) }}
          </p>
        </article>
      </div>

      <div class="mb-4 flex flex-wrap items-center justify-between gap-2 lg:hidden">
        <div class="inline-flex rounded-md border border-line bg-surface p-0.5" role="group">
          <button
            type="button"
            class="rounded-sm px-3 py-1.5 text-sm"
            [class.bg-accent]="viewMode() === 'list'"
            [class.text-on-accent]="viewMode() === 'list'"
            [class.text-fg-2]="viewMode() !== 'list'"
            (click)="viewMode.set('list')"
          >
            {{ 'attendance.myMonth.showList' | transloco }}
          </button>
          <button
            type="button"
            class="rounded-sm px-3 py-1.5 text-sm"
            [class.bg-accent]="viewMode() === 'calendar'"
            [class.text-on-accent]="viewMode() === 'calendar'"
            [class.text-fg-2]="viewMode() !== 'calendar'"
            (click)="viewMode.set('calendar')"
          >
            {{ 'attendance.myMonth.showCalendar' | transloco }}
          </button>
        </div>
      </div>

      @if (m.days.length === 0) {
        <kadr-empty-state
          [title]="'attendance.myMonth.emptyTitle' | transloco"
          [description]="'attendance.myMonth.emptyBody' | transloco"
        />
      } @else {
        <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.35fr)]">
          <!-- Day detail (desktop always; mobile via drawer) -->
          <section
            class="hidden rounded-lg border border-line bg-surface p-4 text-start shadow-sm lg:block"
            [attr.aria-label]="'attendance.myMonth.dayDetail' | transloco"
          >
            @if (selected(); as day) {
              <ng-container [ngTemplateOutlet]="dayDetail" [ngTemplateOutletContext]="{ $implicit: day }" />
            } @else {
              <div class="flex min-h-64 flex-col items-center justify-center gap-2 text-center">
                <kadr-icon name="calendar" class="text-fg-muted" />
                <p class="text-sm text-fg-2">{{ 'attendance.myMonth.pickDay' | transloco }}</p>
              </div>
            }
          </section>

          <section class="rounded-lg border border-line bg-surface p-3 shadow-sm sm:p-4">
            <!-- Mobile list -->
            @if (viewMode() === 'list') {
              <ul class="flex flex-col gap-2 lg:hidden">
                @for (day of m.days; track day.date) {
                  <li>
                    <button
                      type="button"
                      class="flex w-full items-center justify-between gap-3 rounded-lg border border-line bg-app px-3 py-3 text-start hover:bg-hover"
                      (click)="openDay(day)"
                    >
                      <div class="min-w-0">
                        <p class="text-sm font-medium text-fg">{{ dayHeading(day.date) }}</p>
                        <p class="mt-0.5 text-xs tabular-nums text-fg-muted">
                          {{ day.firstInAt ? formatTime(day.firstInAt) : '—' }}
                          ·
                          {{ day.lastOutAt ? formatTime(day.lastOutAt) : '—' }}
                        </p>
                      </div>
                      <kadr-badge [tone]="statusTone(day.status)" [label]="statusLabel(day.status)" />
                    </button>
                  </li>
                }
              </ul>
            }

            <!-- Calendar: always on desktop; toggle on mobile -->
            <div [class.hidden]="viewMode() === 'list'" class="lg:block">
              <div class="mb-2 grid grid-cols-7 gap-1 text-center text-xs text-fg-muted">
                @for (wd of weekdayLabels(); track wd) {
                  <span>{{ wd }}</span>
                }
              </div>
              <div class="grid grid-cols-7 gap-1">
                @for (day of calendarCells(); track $index) {
                  @if (day) {
                    <button
                      type="button"
                      class="flex min-h-14 flex-col items-stretch gap-0.5 rounded-md border p-1 text-start transition-colors sm:min-h-16"
                      [class]="cellClasses(day)"
                      [attr.aria-pressed]="selected()?.date === day.date"
                      (click)="openDay(day)"
                    >
                      <span class="text-xs font-medium tabular-nums text-fg">{{
                        day.date.slice(-2)
                      }}</span>
                      <span class="truncate text-xs leading-tight text-fg-2">{{
                        statusShort(day)
                      }}</span>
                      @if (day.workedMinutes > 0) {
                        <span class="text-xs tabular-nums text-fg-muted">{{
                          hoursLabel(day.workedMinutes)
                        }}</span>
                      }
                    </button>
                  } @else {
                    <div class="min-h-14 sm:min-h-16"></div>
                  }
                }
              </div>
              <ul
                class="mt-4 flex flex-wrap gap-3 border-t border-line pt-3 text-xs text-fg-2"
                [attr.aria-label]="'attendance.myMonth.legend' | transloco"
              >
                <li class="inline-flex items-center gap-1.5">
                  <span class="size-2.5 rounded-full bg-success" aria-hidden="true"></span>
                  {{ 'attendance.day.Present' | transloco }}
                </li>
                <li class="inline-flex items-center gap-1.5">
                  <span class="size-2.5 rounded-full bg-warning" aria-hidden="true"></span>
                  {{ 'attendance.day.Late' | transloco }}
                </li>
                <li class="inline-flex items-center gap-1.5">
                  <span class="size-2.5 rounded-full bg-danger" aria-hidden="true"></span>
                  {{ 'attendance.day.Absent' | transloco }}
                </li>
                <li class="inline-flex items-center gap-1.5">
                  <span class="size-2.5 rounded-full bg-info" aria-hidden="true"></span>
                  {{ 'attendance.day.Holiday' | transloco }}
                </li>
                <li class="inline-flex items-center gap-1.5">
                  <span class="size-2.5 rounded-full bg-fg-muted" aria-hidden="true"></span>
                  {{ 'attendance.day.Weekend' | transloco }}
                </li>
              </ul>
            </div>
          </section>
        </div>
      }
    }

    <kadr-drawer
      [open]="drawerOpen()"
      [title]="selected()?.date ? dayHeading(selected()!.date) : ''"
      [closeLabel]="'ui.close' | transloco"
      (closed)="closeDrawer()"
    >
      @if (selected(); as day) {
        <div class="p-4">
          <ng-container [ngTemplateOutlet]="dayDetail" [ngTemplateOutletContext]="{ $implicit: day }" />
        </div>
      }
    </kadr-drawer>

    <ng-template #dayDetail let-day>
      <div class="flex flex-col gap-4 text-start">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div>
            <p class="text-sm font-medium text-fg">{{ dayHeading(day.date) }}</p>
            <p class="mt-0.5 text-xs text-fg-muted">{{ day.date }}</p>
          </div>
          <kadr-badge [tone]="statusTone(day.status)" [label]="statusLabel(day.status)" />
        </div>

        <div class="rounded-md border border-line bg-app p-3">
          <p class="text-xs text-fg-muted">{{ 'attendance.myMonth.worked' | transloco }}</p>
          <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ hoursLabel(day.workedMinutes) }}</p>
          @if (day.lateMinutes > 0) {
            <p class="mt-1 text-xs text-warning">
              {{ 'attendance.card.lateChip' | transloco: { minutes: day.lateMinutes } }}
            </p>
          }
        </div>

        <div>
          <p class="mb-2 text-sm font-medium text-fg">{{ 'attendance.myMonth.punchLog' | transloco }}</p>
          <ol class="flex flex-col gap-2">
            <li class="flex items-start gap-3 rounded-md border border-line bg-app px-3 py-2">
              <kadr-icon name="fingerprint" size="sm" class="mt-0.5 text-success" />
              <div class="min-w-0">
                <p class="text-sm font-medium text-fg">{{ 'glossary.clockIn' | transloco }}</p>
                <p class="text-xs tabular-nums text-fg-2">
                  {{ day.firstInAt ? formatTime(day.firstInAt) : '—' }}
                </p>
              </div>
            </li>
            <li class="flex items-start gap-3 rounded-md border border-line bg-app px-3 py-2">
              <kadr-icon name="fingerprint" size="sm" class="mt-0.5 text-fg-muted" />
              <div class="min-w-0">
                <p class="text-sm font-medium text-fg">{{ 'glossary.clockOut' | transloco }}</p>
                <p class="text-xs tabular-nums text-fg-2">
                  {{ day.lastOutAt ? formatTime(day.lastOutAt) : '—' }}
                </p>
              </div>
            </li>
          </ol>
        </div>

        <kadr-button variant="secondary" type="button" (click)="openCorrection()">
          {{ 'attendance.corrections.requestCta' | transloco }}
        </kadr-button>
      </div>
    </ng-template>

    <kadr-correction-request-drawer
      [open]="correctionOpen()"
      [initialDate]="selected()?.date ?? null"
      (closed)="correctionOpen.set(false)"
      (submitted)="reload()"
    />
  `,
})
export class MyMonthPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly correctionDrawer = viewChild(CorrectionRequestDrawerComponent);

  protected readonly state = signal<PageState>('loading');
  protected readonly month = signal<AttendanceMonth | null>(null);
  protected readonly year = signal(new Date().getFullYear());
  protected readonly monthNum = signal(new Date().getMonth() + 1);
  protected readonly viewMode = signal<ViewMode>('list');
  protected readonly selected = signal<AttendanceMonthDay | null>(null);
  protected readonly drawerOpen = signal(false);
  protected readonly correctionOpen = signal(false);

  protected readonly monthLabel = computed(() => {
    const d = new Date(this.year(), this.monthNum() - 1, 1);
    return d.toLocaleDateString(this.transloco.getActiveLang() === 'en' ? 'en' : 'ar', {
      month: 'long',
      year: 'numeric',
    });
  });

  protected readonly workdayCount = computed(() => {
    const m = this.month();
    if (!m) {
      return 0;
    }
    return m.days.filter(
      (d) => d.status !== 'Weekend' && d.status !== 'Holiday' && d.status !== 'NoShift',
    ).length;
  });

  protected readonly calendarCells = computed(() => {
    const m = this.month();
    if (!m) {
      return [];
    }
    const first = new Date(m.year, m.month - 1, 1);
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

  protected weekdayLabels(): string[] {
    const keys = ['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat'] as const;
    return keys.map((k) => this.transloco.translate(`attendance.settings.day.${k}`));
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
    this.selected.set(null);
    this.drawerOpen.set(false);
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const data = await this.api.getMyMonth(this.year(), this.monthNum());
      this.month.set(data);
      const todayKey = this.todayKey();
      const todayRow = data.days.find((d) => d.date === todayKey) ?? data.days.at(-1) ?? null;
      this.selected.set(todayRow);
      this.state.set('ready');
    } catch (err) {
      const code = problemDetailsFromError(err)?.code;
      if (code === 'attendance.no_employee') {
        this.month.set(null);
        this.state.set('noEmployee');
        return;
      }
      this.state.set('error');
    }
  }

  protected openDay(day: AttendanceMonthDay): void {
    this.selected.set(day);
    // Drawer on small screens; desktop uses side panel
    if (typeof window !== 'undefined' && window.matchMedia('(max-width: 1023px)').matches) {
      this.drawerOpen.set(true);
    }
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  protected openCorrection(): void {
    this.correctionOpen.set(true);
    queueMicrotask(() => this.correctionDrawer()?.resetFromInputs());
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

  protected statusTone(status: string): BadgeTone {
    switch (status) {
      case 'Present':
        return 'success';
      case 'Late':
      case 'Incomplete':
        return 'warning';
      case 'Absent':
        return 'danger';
      case 'Holiday':
      case 'Leave':
        return 'info';
      default:
        return 'neutral';
    }
  }

  protected statusShort(day: AttendanceMonthDay): string {
    if (day.status === 'Late' && day.lateMinutes > 0) {
      return this.transloco.translate('attendance.myMonth.lateShort', { minutes: day.lateMinutes });
    }
    return this.statusLabel(day.status);
  }

  protected cellClasses(day: AttendanceMonthDay): string {
    const selected = this.selected()?.date === day.date ? ' ring-2 ring-accent' : '';
    const base = 'border-line';
    switch (day.status) {
      case 'Present':
        return `${base} bg-success-soft${selected}`;
      case 'Late':
      case 'Incomplete':
        return `${base} bg-warning-soft${selected}`;
      case 'Absent':
        return `${base} bg-danger-soft${selected}`;
      case 'Holiday':
      case 'Leave':
        return `${base} bg-info-soft${selected}`;
      case 'Weekend':
        return `${base} bg-hover text-fg-muted${selected}`;
      default:
        return `${base} bg-app${selected}`;
    }
  }

  protected dayHeading(dateIso: string): string {
    const [y, m, d] = dateIso.split('-').map(Number);
    const date = new Date(y, m - 1, d);
    return date.toLocaleDateString(this.transloco.getActiveLang() === 'en' ? 'en' : 'ar', {
      weekday: 'long',
      day: 'numeric',
      month: 'short',
    });
  }

  protected formatTime(iso: string): string {
    return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  }

  private todayKey(): string {
    const n = new Date();
    return `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, '0')}-${String(n.getDate()).padStart(2, '0')}`;
  }
}
