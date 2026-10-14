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
import { AttendanceApi, AttendanceRules } from '../../../core/api/attendance.api';
import { TenantTemplatesApi } from '../../../core/api/tenant-templates.api';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { NetworkErrorComponent } from '../../../shared/ui/pwa/network-error.component';
import { SelectComponent, SelectOption } from '../../../shared/ui/select/select.component';
import { SkeletonComponent } from '../../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-attendance-rules-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    IconComponent,
    NetworkErrorComponent,
    SelectComponent,
    SkeletonComponent,
  ],
  template: `

    <header class="mb-6 text-start">
      <h1 class="text-2xl font-bold text-fg">{{ 'attendance.settings.rulesTitle' | transloco }}</h1>
      <p class="mt-1 text-sm text-fg-2">{{ 'attendance.settings.rulesSubtitle' | transloco }}</p>
    </header>

    @if (state() === 'loading') {
      <div class="flex flex-col gap-3">
        <kadr-skeleton height="8rem" />
        <kadr-skeleton height="8rem" />
        <kadr-skeleton height="8rem" />
      </div>
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.settings.loadErrorTitle' | transloco"
        [description]="'attendance.settings.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (rules(); as r) {
      <form class="flex flex-col gap-4 pb-24 text-start" (ngSubmit)="save()">
        <section class="rounded-lg border border-line bg-surface p-4 shadow-sm sm:p-5">
          <h2 class="mb-3 text-base font-bold text-fg">
            {{ 'attendance.settings.rulesSectionDefaultShift' | transloco }}
          </h2>
          <p class="mb-3 text-xs text-fg-muted">{{ 'attendance.settings.defaultShiftHint' | transloco }}</p>
          <kadr-select
            [label]="'attendance.settings.defaultShift' | transloco"
            [options]="shiftOptions()"
            [value]="defaultShiftId()"
            (valueChange)="onDefaultShiftChange($event)"
          />
        </section>

        <section class="rounded-lg border border-line bg-surface p-4 shadow-sm sm:p-5">
          <h2 class="mb-3 text-base font-bold text-fg">
            {{ 'attendance.settings.rulesSectionGrace' | transloco }}
          </h2>
          <div class="grid gap-4 sm:grid-cols-2">
            <label class="flex flex-col gap-1 text-sm">
              <span class="text-fg-2">{{ 'attendance.settings.graceLate' | transloco }}</span>
              <input
                type="number"
                class="rounded-md border border-line bg-app px-3 py-2 text-fg"
                [(ngModel)]="r.graceLateMinutes"
                name="graceLate"
              />
            </label>
            <label class="flex flex-col gap-1 text-sm">
              <span class="text-fg-2">{{ 'attendance.settings.graceEarly' | transloco }}</span>
              <input
                type="number"
                class="rounded-md border border-line bg-app px-3 py-2 text-fg"
                [(ngModel)]="r.graceEarlyLeaveMinutes"
                name="graceEarly"
              />
            </label>
          </div>
        </section>

        <section class="rounded-lg border border-line bg-surface p-4 shadow-sm sm:p-5">
          <h2 class="mb-3 text-base font-bold text-fg">
            {{ 'attendance.settings.rulesSectionAbsent' | transloco }}
          </h2>
          <label class="flex max-w-md flex-col gap-1 text-sm">
            <span class="text-fg-2">{{ 'attendance.settings.absentAfter' | transloco }}</span>
            <input
              type="number"
              class="rounded-md border border-line bg-app px-3 py-2 text-fg"
              [(ngModel)]="r.absentAfterStartHours"
              name="absentAfter"
            />
          </label>
        </section>

        <section class="rounded-lg border border-line bg-surface p-4 shadow-sm sm:p-5">
          <h2 class="mb-3 text-base font-bold text-fg">
            {{ 'attendance.settings.rulesSectionOvertime' | transloco }}
          </h2>
          <label class="mb-3 flex items-center gap-2 text-sm text-fg">
            <input type="checkbox" [(ngModel)]="r.overtimeEnabled" name="otEnabled" />
            {{ 'attendance.settings.overtimeEnabled' | transloco }}
          </label>
          <div class="grid gap-4 sm:grid-cols-2" [class.opacity-50]="!r.overtimeEnabled">
            <label class="flex flex-col gap-1 text-sm">
              <span class="text-fg-2">{{ 'attendance.settings.overtimeAfter' | transloco }}</span>
              <input
                type="number"
                class="rounded-md border border-line bg-app px-3 py-2 text-fg"
                [(ngModel)]="r.overtimeAfterEndMinutes"
                name="otAfter"
                [disabled]="!r.overtimeEnabled"
              />
            </label>
            <label class="flex flex-col gap-1 text-sm">
              <span class="text-fg-2">{{ 'attendance.settings.overtimeMin' | transloco }}</span>
              <input
                type="number"
                class="rounded-md border border-line bg-app px-3 py-2 text-fg"
                [(ngModel)]="r.overtimeMinMinutes"
                name="otMin"
                [disabled]="!r.overtimeEnabled"
              />
            </label>
          </div>
        </section>

        <section class="rounded-lg border border-line bg-surface p-4 shadow-sm sm:p-5">
          <h2 class="mb-3 text-base font-bold text-fg">
            {{ 'attendance.settings.rulesSectionLocation' | transloco }}
          </h2>
          <label class="flex max-w-md flex-col gap-1 text-sm">
            <span class="text-fg-2">{{ 'attendance.settings.locationAccuracy' | transloco }}</span>
            <input
              type="number"
              class="rounded-md border border-line bg-app px-3 py-2 text-fg"
              [(ngModel)]="r.locationAccuracyM"
              name="accuracy"
            />
          </label>
        </section>

        <div
          class="sticky bottom-0 z-10 -mx-1 flex flex-wrap items-center justify-between gap-3 border-t border-line bg-surface/95 px-3 py-3 backdrop-blur supports-[backdrop-filter]:bg-surface/80"
        >
          <p class="inline-flex items-start gap-2 text-xs text-fg-2">
            <kadr-icon name="info" size="sm" class="mt-0.5 shrink-0" />
            {{ 'attendance.settings.rulesFutureNote' | transloco }}
          </p>
          <div class="flex flex-wrap gap-2">
            <kadr-button variant="secondary" type="button" (click)="reload()" [disabled]="saving()">
              {{ 'ui.cancel' | transloco }}
            </kadr-button>
            <kadr-button variant="primary" type="submit" [loading]="saving()">
              {{ 'ui.save' | transloco }}
            </kadr-button>
          </div>
        </div>
      </form>
    }
  `,
})
export class AttendanceRulesPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly templatesApi = inject(TenantTemplatesApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly rules = signal<AttendanceRules | null>(null);
  protected readonly saving = signal(false);
  protected readonly defaultShiftId = signal('');
  protected readonly shiftOptions = signal<SelectOption[]>([]);

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.settings.rulesTitle'));
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const [rules, def, shifts] = await Promise.all([
        this.api.getRules(),
        this.api.getDefaultShift(),
        this.templatesApi.listSection('shifts', { page: 1, pageSize: 100, active: true }),
      ]);
      this.rules.set(rules);
      this.defaultShiftId.set(def.shiftId ?? '');
      const lang = this.transloco.getActiveLang();
      this.shiftOptions.set([
        {
          value: '',
          label: this.transloco.translate('attendance.settings.defaultShiftNone'),
        },
        ...shifts.items.map((s) => ({
          value: s.id,
          label: lang === 'en' ? s.nameEn || s.nameAr : s.nameAr || s.nameEn,
        })),
      ]);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected async onDefaultShiftChange(value: string): Promise<void> {
    this.defaultShiftId.set(value);
    try {
      const saved = await this.api.setDefaultShift(value || null);
      this.defaultShiftId.set(saved.shiftId ?? '');
      this.toast.show(this.transloco.translate('attendance.settings.defaultShiftSaved'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('attendance.settings.saveFailed'), 'danger');
      await this.reload();
    }
  }

  protected async save(): Promise<void> {
    const r = this.rules();
    if (!r) {
      return;
    }
    this.saving.set(true);
    try {
      this.rules.set(await this.api.updateRules(r));
      this.toast.show(this.transloco.translate('attendance.settings.saved'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('attendance.settings.saveFailed'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }
}
