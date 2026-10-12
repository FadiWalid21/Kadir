import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  NotificationPreferenceDto,
  NotificationsApi,
} from '../../core/api/notifications.api';
import { PushSubscriptionDeviceDto } from '../../core/api/push-subscriptions.api';
import { InstallPromptService } from '../../core/pwa/install-prompt.service';
import { PushNotificationService } from '../../core/pwa/push-notification.service';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';

type State = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-notification-preferences-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    DatePipe,
    PageHeaderComponent,
    ButtonComponent,
    EmptyStateComponent,
    SkeletonComponent,
    NetworkErrorComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-xl flex-col gap-4">
      <kadr-page-header
        [title]="'notifications.prefsTitle' | transloco"
        [subtitle]="'notifications.prefsSubtitle' | transloco"
      ></kadr-page-header>

      @if (state() === 'loading') {
        <div class="flex flex-col gap-3" role="status" [attr.aria-label]="'ui.loading' | transloco">
          <kadr-skeleton height="8rem" />
          <kadr-skeleton height="8rem" />
        </div>
      } @else if (state() === 'error') {
        <kadr-network-error
          [title]="'notifications.loadErrorTitle' | transloco"
          [description]="'notifications.loadErrorBody' | transloco"
          (retry)="load()"
        />
      } @else if (items().length === 0) {
        <kadr-empty-state
          [title]="'notifications.emptyTitle' | transloco"
          [description]="'notifications.prefsEmpty' | transloco"
        />
      } @else {
        <section class="rounded-md border border-line bg-surface p-4" aria-labelledby="push-device-heading">
          <h2 id="push-device-heading" class="mb-2 text-sm font-bold text-fg">
            {{ 'notifications.pushDevicesTitle' | transloco }}
          </h2>
          <p class="mb-3 text-sm text-fg-2">{{ pushStatusHint() }}</p>

          @if (push.state() === 'ios-tab') {
            <kadr-button variant="secondary" type="button" (click)="push.openIosInstallHelp()">
              {{ 'notifications.pushInstallFirst' | transloco }}
            </kadr-button>
          } @else if (push.state() !== 'unsupported' && push.state() !== 'subscribed') {
            <kadr-button
              variant="primary"
              type="button"
              [loading]="enabling()"
              (click)="enablePush()"
            >
              {{ 'notifications.enablePush' | transloco }}
            </kadr-button>
          }

          @if (devices().length > 0) {
            <ul class="mt-4 flex flex-col gap-2">
              @for (device of devices(); track device.id) {
                <li class="flex items-center justify-between gap-3 rounded-md border border-line px-3 py-2">
                  <div class="min-w-0">
                    <p class="truncate text-sm font-medium text-fg">
                      {{ device.deviceLabel || ('notifications.pushUnknownDevice' | transloco) }}
                    </p>
                    <p class="text-xs text-fg-2">
                      {{ device.lastSeenAt | date: 'medium' }}
                      @if (!device.isActive) {
                        · {{ 'notifications.pushInactive' | transloco }}
                      }
                    </p>
                  </div>
                  <kadr-button
                    variant="danger-soft"
                    size="sm"
                    type="button"
                    [attr.aria-label]="'notifications.pushRemoveDevice' | transloco"
                    (click)="removeDevice(device.id)"
                  >
                    {{ 'notifications.pushRemove' | transloco }}
                  </kadr-button>
                </li>
              }
            </ul>
            <div class="mt-3">
              <kadr-button
                variant="secondary"
                type="button"
                [loading]="testing()"
                (click)="sendTest()"
              >
                {{ 'notifications.pushSendTest' | transloco }}
              </kadr-button>
            </div>
          }
        </section>

        <ul class="flex flex-col gap-3">
          @for (item of items(); track item.eventType) {
            <li class="rounded-md border border-line bg-surface p-4">
              <p class="mb-3 text-sm font-bold text-fg">
                {{ 'notifications.events.' + item.eventType | transloco }}
              </p>
              <div class="flex flex-col gap-2">
                <label class="flex items-center justify-between gap-3 text-sm text-fg">
                  <span>{{ 'notifications.channel.inApp' | transloco }}</span>
                  <input
                    type="checkbox"
                    class="size-5 accent-[var(--accent)]"
                    [checked]="item.inAppEnabled"
                    (change)="toggle(item.eventType, 'inApp', $event)"
                  />
                </label>
                <label class="flex items-center justify-between gap-3 text-sm text-fg">
                  <span>{{ 'notifications.channel.email' | transloco }}</span>
                  <input
                    type="checkbox"
                    class="size-5 accent-[var(--accent)]"
                    [checked]="item.emailEnabled"
                    (change)="toggle(item.eventType, 'email', $event)"
                  />
                </label>
                <label class="flex items-center justify-between gap-3 text-sm text-fg">
                  <span>{{ 'notifications.channel.push' | transloco }}</span>
                  <input
                    type="checkbox"
                    class="size-5 accent-[var(--accent)]"
                    [checked]="item.pushEnabled"
                    (change)="toggle(item.eventType, 'push', $event)"
                  />
                </label>
              </div>
            </li>
          }
        </ul>

        <div class="flex flex-wrap gap-2">
          <kadr-button variant="primary" type="button" [loading]="saving()" (click)="save()">
            {{ 'notifications.savePrefs' | transloco }}
          </kadr-button>
        </div>
      }
    </div>
  `,
})
export class NotificationPreferencesPage implements OnInit {
  private readonly api = inject(NotificationsApi);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  protected readonly push = inject(PushNotificationService);
  private readonly install = inject(InstallPromptService);

  protected readonly state = signal<State>('loading');
  protected readonly items = signal<NotificationPreferenceDto[]>([]);
  protected readonly devices = signal<PushSubscriptionDeviceDto[]>([]);
  protected readonly saving = signal(false);
  protected readonly enabling = signal(false);
  protected readonly testing = signal(false);

  ngOnInit(): void {
    void this.load();
  }

  pushStatusHint(): string {
    switch (this.push.state()) {
      case 'unsupported':
        return this.transloco.translate('notifications.pushUnavailable');
      case 'ios-tab':
        return this.transloco.translate('notifications.pushIosTabHint');
      case 'denied':
        return this.transloco.translate('notifications.pushDenied');
      case 'subscribed':
        return this.transloco.translate('notifications.pushEnabled');
      case 'failed':
        return this.transloco.translate('notifications.pushFailed');
      default:
        return this.transloco.translate('notifications.pushEnableHint');
    }
  }

  async load(): Promise<void> {
    this.state.set('loading');
    try {
      const [prefs, devices] = await Promise.all([
        this.api.getPreferences(),
        this.push.loadDevices().catch(() => [] as PushSubscriptionDeviceDto[]),
      ]);
      this.items.set(prefs.items);
      this.devices.set(devices);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  toggle(eventType: string, channel: 'inApp' | 'email' | 'push', event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.items.update((list) =>
      list.map((item) => {
        if (item.eventType !== eventType) {
          return item;
        }
        if (channel === 'inApp') {
          return { ...item, inAppEnabled: checked };
        }
        if (channel === 'email') {
          return { ...item, emailEnabled: checked };
        }
        return { ...item, pushEnabled: checked };
      }),
    );
  }

  async save(): Promise<void> {
    this.saving.set(true);
    try {
      const result = await this.api.updatePreferences(this.items());
      this.items.set(result.items);
      this.toast.show(this.transloco.translate('notifications.prefsSaved'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('notifications.prefsSaveError'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }

  async enablePush(): Promise<void> {
    this.enabling.set(true);
    try {
      const ok = await this.push.enableFromUserGesture();
      if (ok) {
        this.toast.show(this.transloco.translate('notifications.pushEnabled'), 'success');
        this.devices.set(await this.push.loadDevices());
      } else if (this.push.state() === 'ios-tab') {
        this.install.openHowToInstall();
      } else {
        this.toast.show(this.transloco.translate('notifications.pushFailed'), 'danger');
      }
    } finally {
      this.enabling.set(false);
    }
  }

  async removeDevice(id: string): Promise<void> {
    try {
      await this.push.removeDevice(id);
      this.devices.set(await this.push.loadDevices());
      this.toast.show(this.transloco.translate('notifications.pushDeviceRemoved'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('notifications.pushFailed'), 'danger');
    }
  }

  async sendTest(): Promise<void> {
    this.testing.set(true);
    try {
      await this.push.sendTest();
      this.toast.show(this.transloco.translate('notifications.pushTestSent'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('notifications.pushFailed'), 'danger');
    } finally {
      this.testing.set(false);
    }
  }
}
