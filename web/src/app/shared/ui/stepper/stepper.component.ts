import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

export interface StepperStep {
  id: string;
  /** Translation key or plain label; always rendered through the Transloco pipe. */
  label: string;
}

@Component({
  selector: 'kadr-stepper',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <nav [attr.aria-label]="ariaLabel()" class="w-full">
      <ol class="flex flex-wrap items-center gap-2">
        @for (step of steps(); track step.id; let i = $index) {
          <li class="flex items-center gap-2">
            <span
              class="inline-flex size-8 items-center justify-center rounded-full text-xs font-medium tabular-nums"
              [class.bg-accent]="i === currentIndex()"
              [class.text-on-accent]="i === currentIndex()"
              [class.bg-subtle]="i !== currentIndex()"
              [class.text-fg-2]="i !== currentIndex()"
              [attr.aria-current]="i === currentIndex() ? 'step' : null"
            >
              {{ i + 1 }}
            </span>
            <span
              class="hidden text-sm sm:inline"
              [class.font-medium]="i === currentIndex()"
              [class.text-fg]="i === currentIndex()"
              [class.text-fg-muted]="i !== currentIndex()"
            >
              {{ step.label | transloco }}
            </span>
            @if (i < steps().length - 1) {
              <span class="mx-1 hidden text-fg-muted sm:inline" aria-hidden="true">·</span>
            }
          </li>
        }
      </ol>
      <p class="mt-2 text-xs text-fg-muted sm:hidden">
        {{ steps()[currentIndex()]?.label | transloco }}
      </p>
    </nav>
  `,
})
export class StepperComponent {
  readonly steps = input.required<StepperStep[]>();
  readonly currentIndex = input(0);
  readonly ariaLabel = input('Steps');
}
