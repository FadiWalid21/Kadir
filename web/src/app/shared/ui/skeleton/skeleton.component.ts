import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'kadr-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="animate-pulse rounded-md bg-subtle"
      [class]="className()"
      [style.height]="height()"
      [style.width]="width()"
      aria-hidden="true"
    ></div>
  `,
})
export class SkeletonComponent {
  readonly className = input('');
  readonly height = input('1rem');
  readonly width = input('100%');
}
