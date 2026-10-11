import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

let nextCheckboxId = 0;

@Component({
  selector: 'kadr-checkbox',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="flex cursor-pointer items-start gap-3 text-start" [class.opacity-50]="disabled()">
      <input
        [id]="controlId"
        type="checkbox"
        class="mt-0.5 size-4 shrink-0 rounded-sm border border-line-strong accent-[var(--color-accent)] focus-visible:outline-none disabled:cursor-not-allowed"
        [checked]="checked()"
        [disabled]="disabled()"
        [attr.aria-describedby]="description() ? descId : null"
        (change)="onChange($event)"
      />
      <span class="min-w-0 flex-1">
        <span class="block text-sm font-medium text-fg">{{ label() }}</span>
        @if (description()) {
          <span [id]="descId" class="mt-0.5 block text-xs text-fg-2">{{ description() }}</span>
        }
      </span>
    </label>
  `,
})
export class CheckboxComponent {
  readonly label = input.required<string>();
  readonly description = input<string | undefined>(undefined);
  readonly checked = input(false);
  readonly disabled = input(false);
  readonly checkedChange = output<boolean>();

  protected readonly controlId = `kadr-checkbox-${++nextCheckboxId}`;
  protected readonly descId = `${this.controlId}-desc`;

  protected onChange(event: Event): void {
    this.checkedChange.emit((event.target as HTMLInputElement).checked);
  }
}
