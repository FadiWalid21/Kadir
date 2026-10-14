import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

export interface AccordionItem {
  id: string;
  title: string;
  body: string;
}

@Component({
  selector: 'kadr-accordion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col gap-2" [attr.aria-label]="ariaLabel()">
      @for (item of items(); track item.id) {
        <div class="rounded-md border border-line bg-surface">
          <h3 class="m-0">
            <button
              type="button"
              class="flex w-full items-center justify-between gap-3 px-4 py-3 text-start text-sm font-medium text-fg hover:bg-hover focus-visible:outline-none"
              [attr.id]="'acc-btn-' + item.id"
              [attr.aria-expanded]="openId() === item.id"
              [attr.aria-controls]="'acc-panel-' + item.id"
              (click)="toggle(item.id)"
            >
              <span>{{ item.title }}</span>
              <span
                class="inline-block text-fg-muted transition-transform duration-[var(--dur)] ease-[var(--ease)] motion-reduce:transition-none"
                [class.rotate-180]="openId() === item.id"
                aria-hidden="true"
              >
                ▾
              </span>
            </button>
          </h3>
          @if (openId() === item.id) {
            <div
              [attr.id]="'acc-panel-' + item.id"
              role="region"
              [attr.aria-labelledby]="'acc-btn-' + item.id"
              class="border-t border-line px-4 py-3 text-start text-sm text-fg-2"
            >
              {{ item.body }}
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AccordionComponent {
  readonly items = input.required<AccordionItem[]>();
  readonly ariaLabel = input.required<string>();
  readonly openId = model<string | null>(null);

  toggle(id: string): void {
    this.openId.set(this.openId() === id ? null : id);
  }
}
