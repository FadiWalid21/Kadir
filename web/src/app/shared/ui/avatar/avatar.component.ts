import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'kadr-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="inline-flex shrink-0 items-center justify-center rounded-full bg-accent-soft font-medium text-accent-soft-fg"
      [class]="sizeClass()"
      [attr.aria-hidden]="ariaLabel() ? null : true"
      [attr.aria-label]="ariaLabel() || null"
      role="img"
    >
      {{ initials() }}
    </span>
  `,
})
export class AvatarComponent {
  readonly name = input.required<string>();
  readonly ariaLabel = input<string | undefined>(undefined);
  readonly size = input<'sm' | 'md' | 'lg'>('md');

  protected readonly initials = computed(() => {
    const parts = this.name().trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return '?';
    }
    if (parts.length === 1) {
      return parts[0]!.slice(0, 2).toUpperCase();
    }
    return `${parts[0]![0] ?? ''}${parts[1]![0] ?? ''}`.toUpperCase();
  });

  protected readonly sizeClass = computed(() => {
    switch (this.size()) {
      case 'sm':
        return 'size-8 text-xs';
      case 'lg':
        return 'size-12 text-base';
      default:
        return 'size-10 text-sm';
    }
  });
}
