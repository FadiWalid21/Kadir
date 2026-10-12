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
import { AttendanceApi, Holiday } from '../../../core/api/attendance.api';
import { BadgeComponent } from '../../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { DrawerComponent } from '../../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { NetworkErrorComponent } from '../../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';
type HolidayFilter = 'all' | 'official' | 'custom';

const WEEK_DAYS = ['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat'] as const;

@Component({
  selector: 'kadr-attendance-holidays-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    BadgeComponent,
    ButtonComponent,
    DrawerComponent,
    EmptyStateComponent,
    IconComponent,
    NetworkErrorComponent,
    SkeletonComponent,
  ],
  template: `

    <header class="mb-4 flex flex-wrap items-start justify-between gap-3 text-start">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.settings.holidaysTitle' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.settings.holidaysSubtitle' | transloco }}</p>
      </div>
      <kadr-button variant="primary" type="button" (click)="openCreate()">
        {{ 'attendance.settings.addHoliday' | transloco }}
      </kadr-button>
    </header>

    <div class="mb-4 flex items-center justify-center gap-2">
      <kadr-button
        variant="ghost"
        type="button"
        [attr.aria-label]="'attendance.myMonth.prev' | transloco"
        (click)="shiftYear(-1)"
      >
        <kadr-icon name="chevrons" [rtlFlip]="true" size="sm" />
      </kadr-button>
      <p class="min-w-24 text-center text-sm font-medium tabular-nums text-fg">{{ year() }}</p>
      <kadr-button
        variant="ghost"
        type="button"
        [attr.aria-label]="'attendance.myMonth.next' | transloco"
        (click)="shiftYear(1)"
      >
        <kadr-icon name="chevrons" size="sm" />
      </kadr-button>
    </div>

    <div
      class="mb-4 rounded-md border border-line bg-info-soft px-4 py-3 text-start text-sm text-fg"
      role="note"
    >
      {{ 'attendance.settings.religiousBanner' | transloco }}
    </div>

    <div class="mb-6 rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
      <p class="mb-2 text-sm font-medium text-fg">{{ 'attendance.settings.weekendTitle' | transloco }}</p>
      <div class="flex flex-wrap gap-2">
        @for (d of weekDays; track d) {
          <button
            type="button"
            class="rounded-md border px-3 py-2 text-sm text-fg"
            [class.border-accent]="weekend().includes(d)"
            [class.bg-accent-soft]="weekend().includes(d)"
            [class.border-line]="!weekend().includes(d)"
            (click)="toggleWeekend(d)"
          >
            {{ ('attendance.settings.day.' + d) | transloco }}
          </button>
        }
      </div>
      <div class="mt-3">
        <kadr-button variant="secondary" type="button" [loading]="savingWeekend()" (click)="saveWeekend()">
          {{ 'attendance.settings.saveWeekend' | transloco }}
        </kadr-button>
      </div>
    </div>

    @if (state() === 'ready') {
      <div class="mb-4 flex flex-wrap items-center gap-2 text-sm">
        <span class="rounded-full bg-accent-soft px-3 py-1 font-medium text-accent-soft-fg">
          {{ 'attendance.settings.holidaysCount' | transloco: { count: holidays().length } }}
        </span>
        <span class="text-fg-2">
          {{ 'attendance.settings.weekendSummary' | transloco: { days: weekendLabel() } }}
        </span>
      </div>

      <div class="mb-4 flex flex-wrap items-center gap-2">
        <div class="inline-flex rounded-md border border-line bg-surface p-0.5" role="group">
          @for (f of filters; track f) {
            <button
              type="button"
              class="rounded-sm px-3 py-1.5 text-sm"
              [class.bg-accent]="filter() === f"
              [class.text-on-accent]="filter() === f"
              [class.text-fg-2]="filter() !== f"
              (click)="filter.set(f)"
            >
              {{ filterLabel(f) }}
            </button>
          }
        </div>
        <label class="ms-auto flex min-w-48 flex-1 items-center gap-2 rounded-md border border-line bg-surface px-3 py-2 text-sm sm:max-w-xs">
          <kadr-icon name="search" size="sm" class="text-fg-muted" />
          <input
            type="search"
            class="w-full bg-transparent text-fg outline-none"
            [placeholder]="'attendance.settings.searchHoliday' | transloco"
            [value]="query()"
            (input)="query.set($any($event.target).value)"
          />
        </label>
      </div>
    }

    @if (state() === 'loading') {
      <div class="flex flex-col gap-2">
        <kadr-skeleton height="3.5rem" />
        <kadr-skeleton height="3.5rem" />
        <kadr-skeleton height="3.5rem" />
      </div>
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.settings.loadErrorTitle' | transloco"
        [description]="'attendance.settings.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (filtered().length === 0) {
      <kadr-empty-state
        [title]="'attendance.settings.holidaysEmptyTitle' | transloco"
        [description]="'attendance.settings.holidaysEmptyBody' | transloco"
      />
    } @else {
      <!-- Desktop table -->
      <div class="hidden overflow-x-auto rounded-lg border border-line bg-surface shadow-sm md:block">
        <table class="w-full min-w-[40rem] text-start text-sm">
          <thead class="border-b border-line bg-app text-fg-2">
            <tr>
              <th class="px-4 py-3 font-medium">{{ 'attendance.settings.nameAr' | transloco }}</th>
              <th class="px-4 py-3 font-medium">{{ 'attendance.settings.startsOn' | transloco }}</th>
              <th class="px-4 py-3 font-medium">{{ 'attendance.settings.endsOn' | transloco }}</th>
              <th class="px-4 py-3 font-medium">{{ 'attendance.settings.duration' | transloco }}</th>
              <th class="px-4 py-3 font-medium">{{ 'attendance.settings.category' | transloco }}</th>
              <th class="px-4 py-3 font-medium"></th>
            </tr>
          </thead>
          <tbody>
            @for (h of filtered(); track h.id) {
              <tr class="border-b border-line last:border-0" [class.bg-accent-soft]="!h.isOfficial">
                <td class="px-4 py-3 font-medium text-fg">
                  {{ lang() === 'en' ? h.nameEn : h.nameAr }}
                </td>
                <td class="px-4 py-3 tabular-nums text-fg-2">{{ formatDate(h.startsOn) }}</td>
                <td class="px-4 py-3 tabular-nums text-fg-2">{{ formatDate(h.endsOn) }}</td>
                <td class="px-4 py-3 text-fg-2">{{ durationLabel(h) }}</td>
                <td class="px-4 py-3">
                  <kadr-badge
                    [tone]="h.isOfficial ? 'info' : 'accent'"
                    [label]="
                      (h.isOfficial
                        ? 'attendance.settings.categoryOfficial'
                        : 'attendance.settings.categoryCustom'
                      ) | transloco
                    "
                  />
                </td>
                <td class="px-4 py-3">
                  <div class="flex justify-end gap-2">
                    <kadr-button variant="secondary" type="button" (click)="openEdit(h)">
                      {{ 'attendance.settings.editHoliday' | transloco }}
                    </kadr-button>
                    <kadr-button variant="danger-soft" type="button" (click)="remove(h)">
                      {{ 'ui.delete' | transloco }}
                    </kadr-button>
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <!-- Mobile cards -->
      <ul class="flex flex-col gap-2 md:hidden">
        @for (h of filtered(); track h.id) {
          <li class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
            <div class="flex flex-wrap items-start justify-between gap-2">
              <p class="text-sm font-medium text-fg">
                {{ lang() === 'en' ? h.nameEn : h.nameAr }}
              </p>
              <kadr-badge
                [tone]="h.isOfficial ? 'info' : 'accent'"
                [label]="durationLabel(h)"
              />
            </div>
            <p class="mt-1 text-xs tabular-nums text-fg-muted">
              {{ formatDate(h.startsOn) }}
              @if (h.endsOn !== h.startsOn) {
                – {{ formatDate(h.endsOn) }}
              }
            </p>
            <p class="mt-1 text-xs text-fg-2">
              {{
                (h.isOfficial
                  ? 'attendance.settings.categoryOfficial'
                  : 'attendance.settings.categoryCustom'
                ) | transloco
              }}
            </p>
            <div class="mt-3 flex gap-2">
              <kadr-button variant="secondary" type="button" (click)="openEdit(h)">
                {{ 'attendance.settings.editHoliday' | transloco }}
              </kadr-button>
              <kadr-button variant="danger-soft" type="button" (click)="remove(h)">
                {{ 'ui.delete' | transloco }}
              </kadr-button>
            </div>
          </li>
        }
      </ul>
    }

    <kadr-drawer
      [open]="drawerOpen()"
      [title]="(editingId() ? 'attendance.settings.editHoliday' : 'attendance.settings.addHoliday') | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="drawerOpen.set(false)"
    >
      <form class="flex flex-col gap-3 p-4 text-start" (ngSubmit)="saveHoliday()">
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.nameAr' | transloco }}</span>
          <input class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="formNameAr" name="nameAr" />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.nameEn' | transloco }}</span>
          <input class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="formNameEn" name="nameEn" />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.startsOn' | transloco }}</span>
          <input type="date" class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="formStarts" name="starts" />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.endsOn' | transloco }}</span>
          <input type="date" class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="formEnds" name="ends" />
        </label>
        <label class="flex items-center gap-2 text-sm text-fg">
          <input type="checkbox" [(ngModel)]="formOfficial" name="official" />
          {{ 'attendance.settings.isOfficial' | transloco }}
        </label>
        <label class="flex items-center gap-2 text-sm text-fg">
          <input type="checkbox" [(ngModel)]="formYearly" name="yearly" />
          {{ 'attendance.settings.repeatsYearly' | transloco }}
        </label>
        <kadr-button variant="primary" type="submit" [loading]="savingHoliday()">
          {{ 'ui.save' | transloco }}
        </kadr-button>
      </form>
    </kadr-drawer>
  `,
})
export class AttendanceHolidaysPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly weekDays = WEEK_DAYS;
  protected readonly filters: HolidayFilter[] = ['all', 'official', 'custom'];
  protected readonly state = signal<PageState>('loading');
  protected readonly holidays = signal<Holiday[]>([]);
  protected readonly weekend = signal<string[]>(['fri', 'sat']);
  protected readonly year = signal(new Date().getFullYear());
  protected readonly filter = signal<HolidayFilter>('all');
  protected readonly query = signal('');
  protected readonly savingWeekend = signal(false);
  protected readonly drawerOpen = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly savingHoliday = signal(false);
  protected readonly lang = signal(this.transloco.getActiveLang());

  protected formNameAr = '';
  protected formNameEn = '';
  protected formStarts = '';
  protected formEnds = '';
  protected formOfficial = true;
  protected formYearly = false;

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const f = this.filter();
    return this.holidays().filter((h) => {
      if (f === 'official' && !h.isOfficial) {
        return false;
      }
      if (f === 'custom' && h.isOfficial) {
        return false;
      }
      if (!q) {
        return true;
      }
      return h.nameAr.toLowerCase().includes(q) || h.nameEn.toLowerCase().includes(q) || h.startsOn.includes(q);
    });
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.settings.holidaysTitle'));
    void this.reload();
  }

  protected weekendLabel(): string {
    return this.weekend()
      .map((d) => this.transloco.translate(`attendance.settings.day.${d}`))
      .join(' · ');
  }

  protected filterLabel(f: HolidayFilter): string {
    const key =
      f === 'all'
        ? 'attendance.settings.filterAll'
        : f === 'official'
          ? 'attendance.settings.filterOfficial'
          : 'attendance.settings.filterCustom';
    return this.transloco.translate(key);
  }

  protected shiftYear(delta: number): void {
    this.year.update((y) => y + delta);
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const [holidays, weekend] = await Promise.all([
        this.api.listHolidays(this.year()),
        this.api.getWeekend(),
      ]);
      this.holidays.set(holidays);
      this.weekend.set([...weekend.weekendDays]);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected formatDate(iso: string): string {
    const [y, m, d] = iso.split('-').map(Number);
    return new Date(y, m - 1, d).toLocaleDateString(
      this.transloco.getActiveLang() === 'en' ? 'en' : 'ar',
      { day: 'numeric', month: 'short', year: 'numeric' },
    );
  }

  protected durationLabel(h: Holiday): string {
    const [ys, ms, ds] = h.startsOn.split('-').map(Number);
    const [ye, me, de] = h.endsOn.split('-').map(Number);
    const start = new Date(ys, ms - 1, ds);
    const end = new Date(ye, me - 1, de);
    const days = Math.max(1, Math.round((end.getTime() - start.getTime()) / 86400000) + 1);
    const key = days === 1 ? 'attendance.settings.durationDays' : 'attendance.settings.durationDays_other';
    return this.transloco.translate(key, { count: days });
  }

  protected toggleWeekend(day: string): void {
    this.weekend.update((days) =>
      days.includes(day) ? days.filter((d) => d !== day) : [...days, day],
    );
  }

  protected async saveWeekend(): Promise<void> {
    this.savingWeekend.set(true);
    try {
      const result = await this.api.updateWeekend(this.weekend());
      this.weekend.set([...result.weekendDays]);
      this.toast.show(this.transloco.translate('attendance.settings.saved'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('attendance.settings.saveFailed'), 'danger');
    } finally {
      this.savingWeekend.set(false);
    }
  }

  protected openCreate(): void {
    this.editingId.set(null);
    this.formNameAr = '';
    this.formNameEn = '';
    this.formStarts = '';
    this.formEnds = '';
    this.formOfficial = false;
    this.formYearly = false;
    this.drawerOpen.set(true);
  }

  protected openEdit(h: Holiday): void {
    this.editingId.set(h.id);
    this.formNameAr = h.nameAr;
    this.formNameEn = h.nameEn;
    this.formStarts = h.startsOn;
    this.formEnds = h.endsOn;
    this.formOfficial = h.isOfficial;
    this.formYearly = h.repeatsYearly;
    this.drawerOpen.set(true);
  }

  protected async saveHoliday(): Promise<void> {
    this.savingHoliday.set(true);
    const body = {
      startsOn: this.formStarts,
      endsOn: this.formEnds || this.formStarts,
      nameAr: this.formNameAr,
      nameEn: this.formNameEn,
      isOfficial: this.formOfficial,
      repeatsYearly: this.formYearly,
      isActive: true,
    };
    try {
      const id = this.editingId();
      if (id) {
        await this.api.updateHoliday(id, body);
      } else {
        await this.api.createHoliday(body);
      }
      this.drawerOpen.set(false);
      this.toast.show(this.transloco.translate('attendance.settings.saved'), 'success');
      await this.reload();
    } catch {
      this.toast.show(this.transloco.translate('attendance.settings.saveFailed'), 'danger');
    } finally {
      this.savingHoliday.set(false);
    }
  }

  protected async remove(h: Holiday): Promise<void> {
    try {
      await this.api.deleteHoliday(h.id);
      this.toast.show(this.transloco.translate('attendance.settings.saved'), 'success');
      await this.reload();
    } catch {
      this.toast.show(this.transloco.translate('attendance.settings.saveFailed'), 'danger');
    }
  }
}
