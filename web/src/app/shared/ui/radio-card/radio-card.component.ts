import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'kadr-radio-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      role="radio"
      class="relative flex w-full items-start gap-3 rounded-md border px-3 py-3 text-start transition-colors focus-visible:outline-none disabled:cursor-not-allowed"
      [class.border-accent]="selected() && !disabled()"
      [class.bg-accent-soft]="selected() && !disabled()"
      [class.border-line]="!selected() || disabled()"
      [class.bg-subtle]="disabled()"
      [class.bg-surface]="!selected() && !disabled()"
      [class.opacity-80]="disabled()"
      [attr.aria-checked]="selected()"
      [attr.aria-disabled]="disabled() || null"
      [disabled]="disabled()"
      (click)="onSelect()"
    >
      <span
        class="mt-0.5 inline-flex size-4 shrink-0 items-center justify-center rounded-full border border-line-strong"
        [class.border-accent]="selected() && !disabled()"
        aria-hidden="true"
      >
        @if (selected() && !disabled()) {
          <span class="size-2 rounded-full bg-accent"></span>
        }
      </span>
      <span class="min-w-0 flex-1 pe-6">
        <span class="flex items-center gap-2">
          @if (disabled()) {
            <svg viewBox="0 0 24 24" class="size-3.5 shrink-0 text-fg-muted" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <rect x="5" y="11" width="14" height="10" rx="2" />
              <path d="M8 11V8a4 4 0 0 1 8 0v3" />
            </svg>
          }
          <span class="text-sm font-medium text-fg">{{ title() }}</span>
          <ng-content select="[radioBadge]" />
        </span>
        @if (description()) {
          <span class="mt-1 block text-xs text-fg-2">{{ description() }}</span>
        }
        @if (disabledNote()) {
          <span class="mt-1 block text-xs text-fg-muted">{{ disabledNote() }}</span>
        }
      </span>
      @if (selected() && !disabled()) {
        <span
          class="absolute start-3 top-3 inline-flex size-5 items-center justify-center rounded-full bg-accent text-on-accent"
          aria-hidden="true"
        >
          <svg viewBox="0 0 24 24" class="size-3" fill="none" stroke="currentColor" stroke-width="3">
            <path d="M20 6 9 17l-5-5" />
          </svg>
        </span>
      }
    </button>
  `,
})
export class RadioCardComponent {
  readonly title = input.required<string>();
  readonly description = input<string | undefined>(undefined);
  readonly selected = input(false);
  readonly disabled = input(false);
  readonly disabledNote = input<string | undefined>(undefined);
  readonly selectedChange = output<void>();

  protected onSelect(): void {
    if (!this.disabled()) {
      this.selectedChange.emit();
    }
  }
}
