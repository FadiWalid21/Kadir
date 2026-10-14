import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'kadr-landing-wordmark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <span
      class="inline-block font-bold leading-none"
      [class.text-accent]="tone() === 'brand'"
      [class.text-on-inverse]="tone() === 'inverse'"
      [class.text-xl]="size() === 'md'"
      [class.text-2xl]="size() === 'lg'"
      [class.text-lg]="size() === 'sm'"
    >
      {{ 'app.name' | transloco }}
    </span>
  `,
})
export class LandingWordmarkComponent {
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly tone = input<'brand' | 'inverse'>('brand');
}
