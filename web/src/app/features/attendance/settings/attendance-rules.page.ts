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
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { NetworkErrorComponent } from '../../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../../shared/ui/toast/toast.service';
import { AttendanceSubnavComponent } from '../attendance-subnav.component';
import { AttendanceSettingsSubnavComponent } from './attendance-settings-subnav.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-attendance-rules-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    AttendanceSubnavComponent,
    AttendanceSettingsSubnavComponent,
    ButtonComponent,
    NetworkErrorComponent,
    SkeletonComponent,
  ],
  template: `
    <kadr-attendance-subnav active="settings" />
    <kadr-attendance-settings-subnav active="rules" />

    <header class="mb-6 text-start">
      <h1 class="text-2xl font-bold text-fg">{{ 'attendance.settings.rulesTitle' | transloco }}</h1>
      <p class="mt-1 text-sm text-fg-2">{{ 'attendance.settings.rulesSubtitle' | transloco }}</p>
    </header>

    @if (state() === 'loading') {
      <kadr-skeleton height="16rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.settings.loadErrorTitle' | transloco"
        [description]="'attendance.settings.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (rules(); as r) {
      <form class="flex max-w-xl flex-col gap-4 text-start" (ngSubmit)="save()">
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.graceLate' | transloco }}</span>
          <input
            type="number"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="r.graceLateMinutes"
            name="graceLate"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.graceEarly' | transloco }}</span>
          <input
            type="number"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="r.graceEarlyLeaveMinutes"
            name="graceEarly"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.absentAfter' | transloco }}</span>
          <input
            type="number"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="r.absentAfterStartHours"
            name="absentAfter"
          />
        </label>
        <label class="flex items-center gap-2 text-sm text-fg">
          <input type="checkbox" [(ngModel)]="r.overtimeEnabled" name="otEnabled" />
          {{ 'attendance.settings.overtimeEnabled' | transloco }}
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.overtimeAfter' | transloco }}</span>
          <input
            type="number"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="r.overtimeAfterEndMinutes"
            name="otAfter"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.overtimeMin' | transloco }}</span>
          <input
            type="number"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="r.overtimeMinMinutes"
            name="otMin"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.settings.locationAccuracy' | transloco }}</span>
          <input
            type="number"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="r.locationAccuracyM"
            name="accuracy"
          />
        </label>
        <div>
          <kadr-button variant="primary" type="submit" [loading]="saving()">
            {{ 'ui.save' | transloco }}
          </kadr-button>
        </div>
      </form>
    }
  `,
})
export class AttendanceRulesPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly rules = signal<AttendanceRules | null>(null);
  protected readonly saving = signal(false);

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.settings.rulesTitle'));
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      this.rules.set(await this.api.getRules());
      this.state.set('ready');
    } catch {
      this.state.set('error');
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
