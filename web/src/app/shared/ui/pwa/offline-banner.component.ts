import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { OfflineService } from '../../../core/pwa/offline.service';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'kadr-offline-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, IconComponent],
  template: `
    @if (offline.offline()) {
      <div
        class="flex items-center gap-3 border-b border-line bg-warning-soft px-4 py-2 text-start text-sm text-fg"
        role="status"
        data-testid="offline-banner"
      >
        <span class="text-warning"><kadr-icon name="wifi-off" size="sm" /></span>
        <p class="min-w-0 flex-1">{{ 'pwa.offline.banner' | transloco }}</p>
        <button
          type="button"
          class="hidden shrink-0 font-medium text-accent underline-offset-2 hover:underline focus-visible:outline-none md:inline"
          data-testid="offline-recheck"
          (click)="offline.recheck()"
        >
          {{ 'pwa.offline.recheck' | transloco }}
        </button>
      </div>
    }
  `,
})
export class OfflineBannerComponent {
  protected readonly offline = inject(OfflineService);
}
