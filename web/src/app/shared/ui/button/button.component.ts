import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger-soft';
export type ButtonSize = 'sm' | 'md' | 'lg';

@Component({
  selector: 'kadr-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'inline-flex',
  },
  template: `
    <button
      [attr.type]="type()"
      [disabled]="disabled() || loading()"
      [attr.aria-busy]="loading() || null"
      [attr.aria-label]="ariaLabel() || null"
      [class]="classes()"
    >
      @if (loading()) {
        <span
          class="inline-block size-4 animate-spin rounded-full border-2 border-current border-e-transparent"
          aria-hidden="true"
        ></span>
      }
      <span
        [class.opacity-0]="loading() && !showLabelWhenLoading()"
        [class.sr-only]="loading() && !showLabelWhenLoading()"
      >
        <ng-content />
      </span>
    </button>
  `,
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly disabled = input(false);
  readonly loading = input(false);
  readonly ariaLabel = input<string | undefined>(undefined);
  readonly showLabelWhenLoading = input(true);

  readonly classes = computed(() => {
    const base =
      'inline-flex items-center justify-center gap-2 font-medium transition-colors focus-visible:outline-none disabled:opacity-50 disabled:pointer-events-none rounded-md';
    const sizes: Record<ButtonSize, string> = {
      sm: 'h-8 px-3 text-sm',
      md: 'h-control px-4 text-sm',
      lg: 'h-12 px-5 text-base',
    };
    const variants: Record<ButtonVariant, string> = {
      primary: 'bg-accent text-on-accent hover:bg-accent-hover',
      secondary: 'bg-surface text-fg border border-line-strong hover:bg-subtle',
      ghost: 'bg-transparent text-fg-2 hover:bg-subtle',
      'danger-soft': 'bg-danger-soft text-danger hover:opacity-90',
    };
    return `${base} ${sizes[this.size()]} ${variants[this.variant()]}`;
  });
}
