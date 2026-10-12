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
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  AttendanceApi,
  BranchLocation,
  ShiftAssignmentCell,
  ShiftScheduleWeek,
} from '../../core/api/attendance.api';
import { TenantTemplatesApi, TemplateItem } from '../../core/api/tenant-templates.api';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';

interface Selection {
  employeeId: string;
  workDate: string;
}

@Component({
  selector: 'kadr-attendance-schedules-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
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
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.schedules.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.schedules.subtitle' | transloco }}</p>
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <a
          routerLink="/attendance/schedules/rules"
          class="text-sm font-medium text-accent hover:underline"
        >
          {{ 'attendance.schedules.rulesLink' | transloco }}
        </a>
        <a
          routerLink="/settings/templates"
          class="text-sm font-medium text-accent hover:underline"
        >
          {{ 'attendance.schedules.templatesLink' | transloco }}
        </a>
      </div>
    </header>

    <div class="mb-4 flex flex-wrap items-center gap-2">
      <kadr-button variant="ghost" type="button" (click)="shiftWeek(-7)">‹</kadr-button>
      <p class="min-w-48 text-center text-sm font-medium tabular-nums text-fg">{{ weekLabel() }}</p>
      <kadr-button variant="ghost" type="button" (click)="shiftWeek(7)">›</kadr-button>
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
    </div>

    @if (week(); as w) {
      <div class="mb-4 flex flex-wrap gap-3 text-xs text-fg-2">
        <span>{{ 'attendance.schedules.scheduled' | transloco }}: {{ w.scheduledShifts }}</span>
        <span>{{ 'attendance.schedules.rest' | transloco }}: {{ w.restDays }}</span>
        <span>{{ 'attendance.schedules.conflicts' | transloco }}: {{ w.conflicts }}</span>
      </div>
    }

    @if (state() === 'loading') {
      <kadr-skeleton height="20rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.schedules.loadErrorTitle' | transloco"
        [description]="'attendance.schedules.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (week(); as w) {
      @if (w.employees.length === 0) {
        <kadr-empty-state
          [title]="'attendance.schedules.emptyTitle' | transloco"
          [description]="'attendance.schedules.emptyBody' | transloco"
        />
      } @else {
        <div class="overflow-x-auto rounded-lg border border-line bg-surface shadow-sm">
          <table class="w-full min-w-0 text-start text-sm">
            <thead class="border-b border-line bg-app text-fg-2">
              <tr>
                <th class="sticky start-0 bg-app px-3 py-2 font-medium">
                  {{ 'attendance.today.colEmployee' | transloco }}
                </th>
                @for (d of dayHeaders(); track d.iso) {
                  <th class="px-2 py-2 text-center font-medium">
                    <span class="block">{{ d.label }}</span>
                    <span class="block text-xs tabular-nums text-fg-muted">{{ d.day }}</span>
                  </th>
                }
              </tr>
            </thead>
            <tbody>
              @for (emp of w.employees; track emp.employeeId) {
                <tr class="border-b border-line last:border-0">
                  <td class="sticky start-0 bg-surface px-3 py-2">
                    <p class="font-medium text-fg">{{ displayName(emp.fullNameAr, emp.fullNameEn) }}</p>
                    <p class="text-xs tabular-nums text-fg-muted">{{ emp.employeeNumber }}</p>
                  </td>
                  @for (cell of emp.days; track cell.workDate) {
                    <td class="px-1 py-1">
                      <button
                        type="button"
                        class="flex min-h-14 w-full flex-col items-center justify-center rounded-md border px-1 py-1 text-xs transition-colors"
                        [class.border-accent]="isSelected(emp.employeeId, cell.workDate)"
                        [class.bg-accent-soft]="isSelected(emp.employeeId, cell.workDate)"
                        [class.border-line]="!isSelected(emp.employeeId, cell.workDate)"
                        [class.bg-app]="cell.isHoliday || cell.isDayOff"
                        (click)="toggleSelect(emp.employeeId, cell.workDate)"
                      >
                        @if (cell.isHoliday) {
                          <span class="text-info">{{ 'attendance.day.Holiday' | transloco }}</span>
                        } @else if (cell.isDayOff) {
                          <span class="text-fg-muted">{{ 'attendance.schedules.dayOff' | transloco }}</span>
                        } @else if (cell.shiftId) {
                          <span class="font-medium text-fg">{{ shiftName(cell) }}</span>
                          <span class="tabular-nums text-fg-muted">
                            {{ cell.startTime?.slice(0, 5) }}–{{ cell.endTime?.slice(0, 5) }}
                          </span>
                        } @else {
                          <span class="text-fg-muted">—</span>
                        }
                        @if (cell.hasConflict) {
                          <kadr-badge
                            tone="danger"
                            [label]="'attendance.schedules.conflict' | transloco"
                          />
                        }
                      </button>
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    }

    @if (selection().length > 0) {
      <div
        class="fixed inset-x-0 bottom-0 z-40 border-t border-line bg-surface px-4 py-3 shadow-lg"
        role="region"
        [attr.aria-label]="'attendance.schedules.assignBar' | transloco"
      >
        <div class="mx-auto flex max-w-5xl flex-wrap items-center gap-2">
          <p class="text-sm text-fg-2">
            {{ 'attendance.schedules.selected' | transloco: { count: selection().length } }}
          </p>
          <select
            class="rounded-md border border-line bg-app px-3 py-2 text-sm text-fg"
            [(ngModel)]="assignShiftId"
          >
            <option value="">{{ 'attendance.schedules.pickShift' | transloco }}</option>
            @for (s of shifts(); track s.id) {
              <option [value]="s.id">{{ displayName(s.nameAr, s.nameEn) }}</option>
            }
          </select>
          <kadr-button variant="primary" type="button" [loading]="saving()" (click)="assignShift()">
            {{ 'attendance.schedules.assign' | transloco }}
          </kadr-button>
          <kadr-button variant="secondary" type="button" [loading]="saving()" (click)="assignDayOff()">
            {{ 'attendance.schedules.markDayOff' | transloco }}
          </kadr-button>
          <kadr-button variant="ghost" type="button" (click)="clearSelection()">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
        </div>
      </div>
    }
  `,
})
export class AttendanceSchedulesPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly templates = inject(TenantTemplatesApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly week = signal<ShiftScheduleWeek | null>(null);
  protected readonly branches = signal<BranchLocation[]>([]);
  protected readonly shifts = signal<TemplateItem[]>([]);
  protected readonly selection = signal<Selection[]>([]);
  protected readonly saving = signal(false);
  protected readonly weekStart = signal(saturdayStart(new Date()));
  protected branchFilter = '';
  protected assignShiftId = '';

  protected readonly weekLabel = computed(() => {
    const w = this.week();
    if (!w) return this.weekStart();
    return `${w.weekStart} – ${w.weekEnd}`;
  });

  protected readonly dayHeaders = computed(() => {
    const w = this.week();
    if (!w || w.employees.length === 0) return [];
    const lang = this.transloco.getActiveLang() === 'en' ? 'en' : 'ar';
    return w.employees[0].days.map((d) => {
      const [y, m, day] = d.workDate.split('-').map(Number);
      const dt = new Date(y, m - 1, day);
      return {
        iso: d.workDate,
        label: dt.toLocaleDateString(lang, { weekday: 'short' }),
        day: String(day),
      };
    });
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.schedules.title'));
    void this.api.listBranches().then((b) => this.branches.set(b)).catch(() => undefined);
    void this.templates
      .listSection('shifts', { pageSize: 50, active: true })
      .then((p) => this.shifts.set(p.items))
      .catch(() => undefined);
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const data = await this.api.getScheduleWeek(
        this.weekStart(),
        this.branchFilter || undefined,
      );
      this.week.set(data);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected shiftWeek(delta: number): void {
    const [y, m, d] = this.weekStart().split('-').map(Number);
    const dt = new Date(y, m - 1, d + delta);
    this.weekStart.set(saturdayStart(dt));
    this.clearSelection();
    void this.reload();
  }

  protected isSelected(employeeId: string, workDate: string): boolean {
    return this.selection().some((s) => s.employeeId === employeeId && s.workDate === workDate);
  }

  protected toggleSelect(employeeId: string, workDate: string): void {
    const cur = this.selection();
    const idx = cur.findIndex((s) => s.employeeId === employeeId && s.workDate === workDate);
    if (idx >= 0) {
      this.selection.set(cur.filter((_, i) => i !== idx));
    } else {
      this.selection.set([...cur, { employeeId, workDate }]);
    }
  }

  protected clearSelection(): void {
    this.selection.set([]);
    this.assignShiftId = '';
  }

  protected async assignShift(): Promise<void> {
    if (!this.assignShiftId || this.selection().length === 0) return;
    this.saving.set(true);
    try {
      await this.api.upsertAssignments(
        this.selection().map((s) => ({
          employeeId: s.employeeId,
          workDate: s.workDate,
          shiftId: this.assignShiftId,
          isDayOff: false,
        })),
      );
      this.clearSelection();
      this.toast.show(this.transloco.translate('attendance.schedules.saved'), 'success');
      await this.reload();
    } catch {
      this.toast.show(this.transloco.translate('attendance.schedules.saveFailed'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }

  protected async assignDayOff(): Promise<void> {
    if (this.selection().length === 0) return;
    this.saving.set(true);
    try {
      await this.api.upsertAssignments(
        this.selection().map((s) => ({
          employeeId: s.employeeId,
          workDate: s.workDate,
          shiftId: null,
          isDayOff: true,
        })),
      );
      this.clearSelection();
      this.toast.show(this.transloco.translate('attendance.schedules.saved'), 'success');
      await this.reload();
    } catch {
      this.toast.show(this.transloco.translate('attendance.schedules.saveFailed'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }

  protected displayName(ar: string, en: string): string {
    return this.transloco.getActiveLang() === 'en' ? en || ar : ar || en;
  }

  protected shiftName(cell: ShiftAssignmentCell): string {
    return this.displayName(cell.shiftNameAr ?? '', cell.shiftNameEn ?? '');
  }
}

function saturdayStart(date: Date): string {
  const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  while (d.getDay() !== 6) {
    d.setDate(d.getDate() - 1);
  }
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
