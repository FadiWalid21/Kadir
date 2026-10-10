import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'kadr-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="flex flex-col items-start gap-2 rounded-lg border border-dashed border-line bg-surface px-4 py-6 text-start"
      role="status"
    >
      <p class="text-sm font-medium text-fg">{{ title() }}</p>
      @if (description()) {
        <p class="text-sm text-fg-2">{{ description() }}</p>
      }
      <ng-content />
    </div>
  `,
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string | undefined>(undefined);
}
