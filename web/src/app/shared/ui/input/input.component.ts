import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { IconComponent } from '../icon/icon.component';

let nextId = 0;

@Component({
  selector: 'kadr-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconComponent],
  template: `
    <div class="grid w-full gap-2">
      <label [attr.for]="controlId" class="text-sm font-medium text-fg">
        {{ label() }}
        @if (required()) {
          <span class="text-danger" aria-hidden="true"> *</span>
        }
      </label>
      <div class="relative flex w-full items-center">
        <input
          [id]="controlId"
          [attr.type]="effectiveType()"
          [attr.name]="name() || null"
          [attr.placeholder]="placeholder() || null"
          [attr.autocomplete]="autocomplete() || null"
          [attr.aria-invalid]="!!error() || null"
          [attr.aria-required]="required() || null"
          [attr.aria-describedby]="describedBy"
          [attr.readonly]="readOnly() ? '' : null"
          [disabled]="disabled()"
          [value]="value()"
          dir="auto"
          class="h-control w-full rounded-md border bg-surface px-3 text-base text-fg placeholder:text-fg-muted focus-visible:outline-none disabled:opacity-50"
          [class.pe-11]="showToggle()"
          [class.bg-subtle]="readOnly()"
          [class.border-danger]="!!error()"
          [class.border-line-strong]="!error()"
          (input)="onInput($event)"
        />
        @if (showToggle()) {
          <button
            type="button"
            class="absolute end-1 inline-flex size-9 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
            [attr.aria-label]="revealed() ? hideLabel() : showLabel()"
            [attr.aria-pressed]="revealed()"
            (click)="revealed.set(!revealed())"
          >
            <kadr-icon [name]="revealed() ? 'eye-off' : 'eye'" size="sm" />
          </button>
        }
      </div>
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
  readonly readOnly = input(false);
  readonly autocomplete = input<string | undefined>(undefined);
  /** Password fields only: shows a show/hide toggle. Requires both labels for accessibility. */
  readonly revealable = input(false);
  readonly showLabel = input('');
  readonly hideLabel = input('');
  readonly valueChange = output<string>();

  protected readonly revealed = signal(false);
  protected readonly showToggle = computed(() => this.revealable() && this.type() === 'password');
  protected readonly effectiveType = computed(() =>
    this.showToggle() && this.revealed() ? 'text' : this.type(),
  );

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
