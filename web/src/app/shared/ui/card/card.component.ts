import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'kadr-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rounded-lg border border-line bg-surface p-5 shadow-sm">
      @if (title()) {
        <h2 class="mb-3 text-lg font-bold text-fg leading-tight">{{ title() }}</h2>
      }
      <ng-content />
    </section>
  `,
})
export class CardComponent {
  readonly title = input<string | undefined>(undefined);
}
