import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AttendanceApi, Holiday } from '../../../core/api/attendance.api';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { DrawerComponent } from '../../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { AttendanceSubnavComponent } from '../attendance-subnav.component';
import { AttendanceSettingsSubnavComponent } from './attendance-settings-subnav.component';

type PageState = 'loading' | 'ready' | 'error';

const WEEK_DAYS = ['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat'] as const;

@Component({
  selector: 'kadr-attendance-holidays-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    AttendanceSubnavComponent,
    AttendanceSettingsSubnavComponent,
    ButtonComponent,
    DrawerComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
  ],
  template: `
    <kadr-attendance-subnav active="settings" />
    <kadr-attendance-settings-subnav active="holidays" />

    <header class="mb-6 flex flex-wrap items-start justify-between gap-3 text-start">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.settings.holidaysTitle' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.settings.holidaysSubtitle' | transloco }}</p>
      </div>
      <kadr-button variant="primary" type="button" (click)="openCreate()">
        {{ 'attendance.settings.addHoliday' | transloco }}
      </kadr-button>
    </header>

    <div class="mb-6 rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
      <p class="mb-2 text-sm font-medium text-fg">{{ 'attendance.settings.weekendTitle' | transloco }}</p>
      <div class="flex flex-wrap gap-2">
        @for (d of weekDays; track d) {
          <button
            type="button"
            class="rounded-md border px-3 py-2 text-sm"
            [class.border-accent]="weekend().includes(d)"
            [class.bg-hover]="weekend().includes(d)"
            [class.text-fg]="true"
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

    <div
      class="mb-6 rounded-md border border-line bg-app px-4 py-3 text-start text-sm text-fg-2"
      role="note"
    >
      {{ 'attendance.settings.religiousBanner' | transloco }}
    </div>

    @if (state() === 'loading') {
      <kadr-skeleton height="12rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.settings.loadErrorTitle' | transloco"
        [description]="'attendance.settings.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (holidays().length === 0) {
      <kadr-empty-state
        [title]="'attendance.settings.holidaysEmptyTitle' | transloco"
        [description]="'attendance.settings.holidaysEmptyBody' | transloco"
      />
    } @else {
      <ul class="flex flex-col gap-2">
        @for (h of holidays(); track h.id) {
          <li
            class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-line bg-surface px-4 py-3 text-start shadow-sm"
          >
            <div>
              <p class="text-sm font-medium text-fg">
                {{ lang() === 'en' ? h.nameEn : h.nameAr }}
              </p>
              <p class="text-xs text-fg-muted tabular-nums">
                {{ h.startsOn }}
                @if (h.endsOn !== h.startsOn) {
                  – {{ h.endsOn }}
                }
                @if (h.repeatsYearly) {
                  · {{ 'attendance.settings.repeatsYearly' | transloco }}
                }
              </p>
            </div>
            <div class="flex gap-2">
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
  protected readonly state = signal<PageState>('loading');
  protected readonly holidays = signal<Holiday[]>([]);
  protected readonly weekend = signal<string[]>(['fri', 'sat']);
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

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.settings.holidaysTitle'));
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const [holidays, weekend] = await Promise.all([
        this.api.listHolidays(),
        this.api.getWeekend(),
      ]);
      this.holidays.set(holidays);
      this.weekend.set([...weekend.weekendDays]);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
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
