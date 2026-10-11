import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ButtonComponent } from '../button/button.component';

/** Centered confirm dialog for destructive or irreversible actions. */
@Component({
  selector: 'kadr-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonComponent],
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4" role="presentation">
        <button
          type="button"
          class="absolute inset-0 bg-fg/40"
          [attr.aria-label]="cancelLabel()"
          (click)="cancelled.emit()"
        ></button>
        <div
          class="relative z-10 w-full max-w-md rounded-lg border border-line bg-surface p-4 shadow-lg"
          role="alertdialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          [attr.aria-describedby]="bodyId"
        >
          <h2 [id]="titleId" class="text-start text-lg font-bold text-fg">{{ title() }}</h2>
          @if (message()) {
            <p [id]="bodyId" class="mt-2 text-start text-sm text-fg-2">{{ message() }}</p>
          } @else {
            <span [id]="bodyId" class="sr-only">{{ title() }}</span>
          }
          <div class="mt-3">
            <ng-content />
          </div>
          <div class="mt-4 flex flex-wrap justify-end gap-2">
            <kadr-button variant="secondary" type="button" (click)="cancelled.emit()">
              {{ cancelLabel() }}
            </kadr-button>
            <kadr-button
              [variant]="danger() ? 'danger-soft' : 'primary'"
              type="button"
              [loading]="busy()"
              (click)="confirmed.emit()"
            >
              {{ confirmLabel() }}
            </kadr-button>
          </div>
        </div>
      </div>
    }
  `,
})
export class ConfirmDialogComponent {
  readonly open = input(false);
  readonly title = input.required<string>();
  /** Optional body text; projected content can replace or supplement it. */
  readonly message = input('');
  readonly confirmLabel = input.required<string>();
  readonly cancelLabel = input.required<string>();
  readonly danger = input(false);
  readonly busy = input(false);
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  protected readonly titleId = `confirm-title-${Math.random().toString(36).slice(2, 9)}`;
  protected readonly bodyId = `confirm-body-${Math.random().toString(36).slice(2, 9)}`;
}
