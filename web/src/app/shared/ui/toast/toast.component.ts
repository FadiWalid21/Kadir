import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Component({
  selector: 'kadr-toast-host',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pointer-events-none fixed inset-x-0 bottom-20 z-50 flex flex-col items-center gap-2 px-4 md:bottom-6"
      aria-live="polite"
    >
      @for (msg of toast.messages(); track msg.id) {
        <div
          class="pointer-events-auto max-w-sm rounded-md border border-line bg-surface px-4 py-3 text-sm shadow-md"
          [class.text-danger]="msg.tone === 'danger'"
          [class.text-success]="msg.tone === 'success'"
          [class.text-fg]="msg.tone === 'info'"
          role="status"
        >
          {{ msg.text }}
        </div>
      }
    </div>
  `,
})
export class ToastHostComponent {
  protected readonly toast = inject(ToastService);
}
