import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AttendanceApi } from '../../core/api/attendance.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

const PROBLEM_TYPES = ['ForgotIn', 'ForgotOut', 'WrongTime', 'Other'] as const;

@Component({
  selector: 'kadr-correction-request-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslocoPipe, DrawerComponent, ButtonComponent],
  template: `
    <kadr-drawer
      [open]="open()"
      [title]="'attendance.corrections.formTitle' | transloco"
      [subtitle]="'attendance.corrections.formSubtitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      [hasFooter]="false"
      (closed)="closed.emit()"
    >
      <form class="flex flex-col gap-4 text-start" (ngSubmit)="submit()">
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.corrections.workDate' | transloco }}</span>
          <input
            type="date"
            class="rounded-md border border-line bg-app px-3 py-2 text-fg"
            [(ngModel)]="workDate"
            name="workDate"
            required
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.corrections.problemType' | transloco }}</span>
          <select
            class="rounded-md border border-line bg-app px-3 py-2 text-fg"
            [(ngModel)]="problemType"
            name="problemType"
            required
          >
            @for (t of problemTypes; track t) {
              <option [value]="t">{{ ('attendance.corrections.types.' + t) | transloco }}</option>
            }
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.corrections.proposedTime' | transloco }}</span>
          <input
            type="time"
            class="rounded-md border border-line bg-app px-3 py-2 text-fg"
            [(ngModel)]="proposedLocal"
            name="proposedLocal"
            required
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'attendance.corrections.reason' | transloco }}</span>
          <textarea
            class="min-h-24 rounded-md border border-line bg-app px-3 py-2 text-fg"
            [(ngModel)]="reason"
            name="reason"
            required
            minlength="5"
          ></textarea>
        </label>
        @if (error()) {
          <p class="text-sm text-danger" role="alert">{{ error() }}</p>
        }
        <div class="flex justify-end gap-2">
          <kadr-button variant="ghost" type="button" (click)="closed.emit()">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
          <kadr-button variant="primary" type="submit" [loading]="saving()">
            {{ 'attendance.corrections.submit' | transloco }}
          </kadr-button>
        </div>
      </form>
    </kadr-drawer>
  `,
})
export class CorrectionRequestDrawerComponent {
  private readonly api = inject(AttendanceApi);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  readonly open = input(false);
  readonly initialDate = input<string | null>(null);
  readonly closed = output<void>();
  readonly submitted = output<void>();

  protected readonly problemTypes = PROBLEM_TYPES;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected workDate = '';
  protected problemType: (typeof PROBLEM_TYPES)[number] = 'ForgotIn';
  protected proposedLocal = '';
  protected reason = '';

  /** Call when opening so form pre-fills. */
  resetFromInputs(): void {
    this.workDate = this.initialDate() ?? new Date().toISOString().slice(0, 10);
    this.problemType = 'ForgotIn';
    this.proposedLocal = '09:00';
    this.reason = '';
    this.error.set(null);
  }

  protected async submit(): Promise<void> {
    this.error.set(null);
    if (!this.workDate || !this.proposedLocal || this.reason.trim().length < 5) {
      this.error.set(this.transloco.translate('attendance.corrections.validation'));
      return;
    }
    this.saving.set(true);
    try {
      const proposedTime =
        this.proposedLocal.length === 5 ? `${this.proposedLocal}:00` : this.proposedLocal;
      await this.api.createCorrection({
        workDate: this.workDate,
        problemType: this.problemType,
        proposedTime,
        reason: this.reason.trim(),
      });
      this.toast.show(this.transloco.translate('attendance.corrections.submitted'), 'success');
      this.submitted.emit();
      this.closed.emit();
    } catch {
      this.error.set(this.transloco.translate('attendance.corrections.submitFailed'));
    } finally {
      this.saving.set(false);
    }
  }
}
