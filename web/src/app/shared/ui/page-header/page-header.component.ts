import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'kadr-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="mb-6 flex flex-col gap-1 text-start">
      <h1 class="text-2xl font-bold leading-tight text-fg">{{ title() }}</h1>
      @if (subtitle()) {
        <p class="text-sm text-fg-2">{{ subtitle() }}</p>
      }
      <ng-content />
    </header>
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string | undefined>(undefined);
}
