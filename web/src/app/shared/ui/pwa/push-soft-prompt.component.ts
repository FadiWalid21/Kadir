import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../core/auth/auth.service';
import { PushNotificationService } from '../../../core/pwa/push-notification.service';
import { ButtonComponent } from '../button/button.component';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'kadr-push-soft-prompt',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent, IconComponent],
  template: `
    @if (visible()) {
      <div
        class="mb-4 rounded-md border border-line bg-surface p-4"
        role="region"
        data-testid="push-soft-prompt"
        [attr.aria-label]="'notifications.pushPromptRegion' | transloco"
      >
        <div class="flex items-start gap-3">
          <div class="min-w-0 flex-1">
            <p class="text-sm font-bold text-fg">{{ 'notifications.pushPromptTitle' | transloco }}</p>
            <p class="mt-1 text-sm text-fg-2">{{ bodyKey() | transloco }}</p>
          </div>
          <button
            type="button"
            class="inline-flex size-11 shrink-0 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
            [attr.aria-label]="'notifications.pushPromptDismiss' | transloco"
            data-testid="push-soft-prompt-dismiss"
            (click)="push.dismissSoftPrompt()"
          >
            <kadr-icon name="x" size="sm" />
          </button>
        </div>
        <div class="mt-3 flex flex-wrap gap-2">
          <kadr-button
            variant="primary"
            size="sm"
            type="button"
            data-testid="push-soft-prompt-enable"
            (click)="onEnable()"
          >
            {{ 'notifications.enablePush' | transloco }}
          </kadr-button>
          <kadr-button variant="secondary" size="sm" type="button" (click)="push.dismissSoftPrompt()">
            {{ 'notifications.pushPromptLater' | transloco }}
          </kadr-button>
        </div>
      </div>
    }
  `,
})
export class PushSoftPromptComponent {
  protected readonly push = inject(PushNotificationService);
  private readonly auth = inject(AuthService);

  protected readonly visible = computed(
    () => this.auth.status() === 'authenticated' && this.push.softPromptEligible(),
  );

  protected readonly bodyKey = computed(() =>
    this.push.state() === 'failed'
      ? 'notifications.pushPromptBodyFailed'
      : 'notifications.pushPromptBody',
  );

  async onEnable(): Promise<void> {
    await this.push.enableFromUserGesture();
  }
}
