import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { UpdateService } from '../../../core/pwa/update.service';
import { ButtonComponent } from '../button/button.component';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'kadr-update-notice',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent, IconComponent],
  template: `
    @if (visible()) {
      <div
        class="kadr-update-notice fixed inset-x-3 z-40 flex flex-col gap-3 rounded-lg border border-line bg-surface p-4 shadow-md md:inset-x-auto"
        [class.bg-warning-soft]="unsaved()"
        role="status"
        data-testid="update-notice"
        [attr.data-state]="updates.state()"
      >
        <div class="flex items-start gap-3 text-start">
          <span class="mt-0.5 text-accent">
            @if (updates.state() === 'updating') {
              <span
                class="inline-block size-5 animate-spin rounded-full border-2 border-accent border-e-transparent"
                role="status"
                [attr.aria-label]="'pwa.update.updating' | transloco"
              ></span>
            } @else {
              <kadr-icon name="refresh" size="md" />
            }
          </span>
          <div class="min-w-0 flex-1">
            <p class="text-sm font-bold text-fg">{{ 'pwa.update.title' | transloco }}</p>
            <p class="mt-1 text-sm text-fg-2">
              {{ (unsaved() ? 'pwa.update.unsavedBody' : 'pwa.update.body') | transloco }}
            </p>
          </div>
          @if (updates.state() !== 'updating') {
            <button
              type="button"
              class="inline-flex size-11 shrink-0 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
              [attr.aria-label]="'ui.close' | transloco"
              (click)="updates.dismiss()"
            >
              <kadr-icon name="x" size="sm" />
            </button>
          }
        </div>
        <div class="flex flex-wrap items-center justify-end gap-2">
          @if (unsaved() && updates.state() !== 'updating') {
            <kadr-button variant="ghost" type="button" (click)="updates.dismiss()">
              {{ 'pwa.update.later' | transloco }}
            </kadr-button>
          }
          <kadr-button
            variant="primary"
            type="button"
            [loading]="updates.state() === 'updating'"
            [disabled]="updates.state() === 'updating'"
            (click)="updates.applyUpdate()"
          >
            {{ 'pwa.update.now' | transloco }}
          </kadr-button>
        </div>
      </div>
    }
  `,
})
export class UpdateNoticeComponent {
  protected readonly updates = inject(UpdateService);
  protected readonly unsaved = this.updates.hasUnsavedChanges;
  protected readonly visible = computed(
    () => this.updates.state() === 'ready' || this.updates.state() === 'updating',
  );
}
