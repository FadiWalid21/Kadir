import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonComponent } from '../button/button.component';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'kadr-network-error',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent, IconComponent],
  template: `
    <div
      class="flex flex-col items-start gap-3 rounded-lg border border-dashed border-line bg-surface px-4 py-6 text-start"
      role="alert"
      data-testid="network-error"
    >
      <span
        class="inline-flex size-12 items-center justify-center rounded-full bg-warning-soft text-warning"
        aria-hidden="true"
      >
        <kadr-icon name="wifi-off" size="lg" />
      </span>
      <p class="text-sm font-bold text-fg">{{ title() || ('pwa.offline.errorTitle' | transloco) }}</p>
      <p class="text-sm text-fg-2">{{ description() || ('pwa.offline.errorBody' | transloco) }}</p>
      <kadr-button variant="primary" type="button" (click)="retry.emit()">
        {{ 'pwa.offline.retry' | transloco }}
      </kadr-button>
    </div>
  `,
})
export class NetworkErrorComponent {
  readonly title = input<string | undefined>(undefined);
  readonly description = input<string | undefined>(undefined);
  readonly retry = output<void>();
}
