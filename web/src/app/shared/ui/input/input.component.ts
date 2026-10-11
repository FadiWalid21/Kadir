import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

let nextId = 0;

@Component({
  selector: 'kadr-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid w-full gap-2">
      <label [attr.for]="controlId" class="text-sm font-medium text-fg">
        {{ label() }}
        @if (required()) {
          <span class="text-danger" aria-hidden="true"> *</span>
        }
      </label>
      <input
        [id]="controlId"
        [attr.type]="type()"
        [attr.name]="name() || null"
        [attr.placeholder]="placeholder() || null"
        [attr.aria-invalid]="!!error() || null"
        [attr.aria-required]="required() || null"
        [attr.aria-describedby]="describedBy"
        [disabled]="disabled()"
        [value]="value()"
        dir="auto"
        class="h-control w-full rounded-md border bg-surface px-3 text-base text-fg placeholder:text-fg-muted focus-visible:outline-none disabled:opacity-50"
        [class.border-danger]="!!error()"
        [class.border-line-strong]="!error()"
        (input)="onInput($event)"
      />
      @if (error()) {
        <p [id]="errorId" class="text-xs text-danger" role="alert">{{ error() }}</p>
      } @else if (hint()) {
        <p [id]="hintId" class="text-xs text-fg-muted">{{ hint() }}</p>
      }
    </div>
  `,
})
export class InputComponent {
  readonly label = input.required<string>();
  readonly hint = input<string | undefined>(undefined);
  readonly error = input<string | undefined>(undefined);
  readonly type = input('text');
  readonly name = input<string | undefined>(undefined);
  readonly placeholder = input<string | undefined>(undefined);
  readonly value = input('');
  readonly disabled = input(false);
  readonly required = input(false);
  readonly valueChange = output<string>();

  protected readonly controlId = `kadr-input-${++nextId}`;
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

  protected onInput(event: Event): void {
    this.valueChange.emit((event.target as HTMLInputElement).value);
  }
}
