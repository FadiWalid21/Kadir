import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { OfflineService } from '../core/pwa/offline.service';
import { UpdateService } from '../core/pwa/update.service';
import { SubscriptionStore } from '../core/subscription/subscription.store';
import { SeatLimitDialogComponent } from '../shared/ui/seat-limit-dialog/seat-limit-dialog.component';
import { ToastHostComponent } from '../shared/ui/toast/toast.component';
import { PushNotificationService } from '../core/pwa/push-notification.service';
import { OfflineBannerComponent } from '../shared/ui/pwa/offline-banner.component';
import { PushSoftPromptComponent } from '../shared/ui/pwa/push-soft-prompt.component';
import { UpdateNoticeComponent } from '../shared/ui/pwa/update-notice.component';
import { BottomNavComponent } from './bottom-nav/bottom-nav.component';
import { SidebarComponent } from './sidebar/sidebar.component';
import { SubscriptionBannerComponent } from './subscription-banner/subscription-banner.component';
import { TopBarComponent } from './top-bar/top-bar.component';

const SIDEBAR_COLLAPSED_KEY = 'kadr.sidebar.collapsed';

@Component({
  selector: 'kadr-shell-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    SidebarComponent,
    TopBarComponent,
    BottomNavComponent,
    ToastHostComponent,
    SubscriptionBannerComponent,
    SeatLimitDialogComponent,
    OfflineBannerComponent,
    PushSoftPromptComponent,
    UpdateNoticeComponent,
  ],
  template: `
    <div class="flex min-h-dvh bg-app">
      <div class="sticky top-0 hidden h-dvh shrink-0 md:block">
        <kadr-sidebar [collapsed]="collapsed()" (collapsedChange)="onCollapsedChange($event)" />
      </div>
      <div class="flex min-w-0 flex-1 flex-col">
        <kadr-top-bar />
        <kadr-offline-banner />
        <kadr-subscription-banner />
        <main class="mx-auto w-full max-w-screen-xl flex-1 px-6 pb-24 pt-6 md:pb-8">
          <kadr-push-soft-prompt />
          <router-outlet />
        </main>
      </div>
      <kadr-bottom-nav />
      <kadr-update-notice />
      <kadr-toast-host />
      <kadr-seat-limit-dialog
        [open]="subscription.seatDialogOpen()"
        [cap]="subscription.seatCap()"
        [active]="subscription.seatActive()"
        (closed)="subscription.closeSeatLimitDialog()"
      />
    </div>
  `,
})
export class ShellLayout {
  protected readonly subscription = inject(SubscriptionStore);
  readonly collapsed = signal(readCollapsed());

  constructor() {
    inject(OfflineService);
    inject(UpdateService);
    inject(PushNotificationService).startLifecycle();
  }

  onCollapsedChange(value: boolean): void {
    this.collapsed.set(value);
    try {
      localStorage.setItem(SIDEBAR_COLLAPSED_KEY, value ? '1' : '0');
    } catch {
      // Ignore quota / private mode.
    }
  }
}

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === '1';
  } catch {
    return false;
  }
}
