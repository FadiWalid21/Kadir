import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

let nextSelectId = 0;

@Component({
  selector: 'kadr-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid w-full gap-2">
      <label [attr.for]="controlId" class="text-sm font-medium text-fg">{{ label() }}</label>
      <select
        [id]="controlId"
        class="h-control w-full rounded-md border border-line-strong bg-surface px-3 text-base text-fg focus-visible:outline-none disabled:opacity-50"
        [disabled]="disabled()"
        [attr.aria-invalid]="!!error() || null"
        [attr.aria-describedby]="describedBy"
        [value]="value()"
        (change)="onChange($event)"
      >
        @if (placeholder()) {
          <option value="" disabled [selected]="!value()">{{ placeholder() }}</option>
        }
        @for (opt of options(); track opt.value) {
          <option [value]="opt.value" [disabled]="!!opt.disabled">{{ opt.label }}</option>
        }
      </select>
      @if (error()) {
        <p [id]="errorId" class="text-xs text-danger" role="alert">{{ error() }}</p>
      } @else if (hint()) {
        <p [id]="hintId" class="text-xs text-fg-muted">{{ hint() }}</p>
      }
    </div>
  `,
})
export class SelectComponent {
  readonly label = input.required<string>();
  readonly options = input.required<SelectOption[]>();
  readonly value = input('');
  readonly placeholder = input<string | undefined>(undefined);
  readonly hint = input<string | undefined>(undefined);
  readonly error = input<string | undefined>(undefined);
  readonly disabled = input(false);
  readonly valueChange = output<string>();

  protected readonly controlId = `kadr-select-${++nextSelectId}`;
  protected readonly hintId = `${this.controlId}-hint`;
  protected readonly errorId = `${this.controlId}-error`;

  protected get describedBy(): string | null {
    if (this.error()) {
      return this.errorId;
    }
    if (this.hint()) {
      return this.hintId;
    }
    return null;
  }

  protected onChange(event: Event): void {
    this.valueChange.emit((event.target as HTMLSelectElement).value);
  }
}
