import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type BadgeTone = 'success' | 'warning' | 'danger' | 'info' | 'accent' | 'neutral';

@Component({
  selector: 'kadr-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span [class]="classes()" [attr.title]="label()">
      <span class="inline-flex size-3.5 shrink-0" aria-hidden="true">
        @switch (tone()) {
          @case ('success') {
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="size-full">
              <path d="M20 6 9 17l-5-5" />
            </svg>
          }
          @case ('warning') {
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="size-full">
              <circle cx="12" cy="12" r="10" />
              <path d="M12 8v4" />
              <path d="M12 16h.01" />
            </svg>
          }
          @case ('danger') {
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="size-full">
              <path d="M18 6 6 18" />
              <path d="m6 6 12 12" />
            </svg>
          }
          @case ('info') {
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="size-full">
              <circle cx="12" cy="12" r="10" />
              <path d="M12 16v-4" />
              <path d="M12 8h.01" />
            </svg>
          }
          @default {
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="size-full">
              <circle cx="12" cy="12" r="4" />
            </svg>
          }
        }
      </span>
      <span>{{ label() }}</span>
    </span>
  `,
})
export class BadgeComponent {
  readonly tone = input<BadgeTone>('neutral');
  readonly label = input.required<string>();

  readonly classes = computed(() => {
    const base =
      'inline-flex items-center gap-1.5 h-6 px-2.5 rounded-full text-xs font-medium whitespace-nowrap';
    const tones: Record<BadgeTone, string> = {
      success: 'bg-success-soft text-success',
      warning: 'bg-warning-soft text-warning',
      danger: 'bg-danger-soft text-danger',
      info: 'bg-info-soft text-info',
      accent: 'bg-accent-soft text-accent-soft-fg',
      neutral: 'bg-subtle text-fg-2',
    };
    return `${base} ${tones[this.tone()]}`;
  });
}
