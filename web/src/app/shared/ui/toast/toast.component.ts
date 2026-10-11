import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ToastService } from './toast.service';

@Component({
  selector: 'kadr-toast-host',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div
      class="kadr-toast-stack pointer-events-none fixed inset-x-0 z-50 flex flex-col items-center gap-2 px-4"
      aria-live="polite"
    >
      @for (msg of toast.messages(); track msg.id) {
        <div
          class="pointer-events-auto flex max-w-sm flex-col gap-1 rounded-md border border-line bg-surface px-4 py-3 text-sm shadow-md"
          [class.text-danger]="msg.tone === 'danger'"
          [class.text-success]="msg.tone === 'success'"
          [class.text-fg]="msg.tone === 'info'"
          role="status"
        >
          <span>{{ msg.text }}</span>
          @if (msg.link) {
            @if (msg.link.url.startsWith('/') && !msg.link.url.startsWith('//')) {
              <a
                [routerLink]="msg.link.url"
                class="font-medium text-accent underline underline-offset-2"
                (click)="toast.dismiss(msg.id)"
              >
                {{ msg.link.label }}
              </a>
            } @else {
              <a
                [href]="msg.link.url"
                target="_blank"
                rel="noopener"
                class="font-medium text-accent underline underline-offset-2"
                (click)="toast.dismiss(msg.id)"
              >
                {{ msg.link.label }}
              </a>
            }
          }
        </div>
      }
    </div>
  `,
})
export class ToastHostComponent {
  protected readonly toast = inject(ToastService);
}
