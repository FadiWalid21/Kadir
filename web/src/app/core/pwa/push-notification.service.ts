import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, NgZone, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SwPush } from '@angular/service-worker';
import { firstValueFrom } from 'rxjs';
import { PushSubscriptionsApi } from '../api/push-subscriptions.api';
import { InstallPromptService } from './install-prompt.service';
import { resolveNotificationDeepLink } from './notification-deep-link';

const PROMPT_DISMISS_KEY = 'kadr.push.softPromptDismissed';
const DEVICE_ID_KEY = 'kadr.push.deviceId';

export type PushUiState =
  | 'unsupported'
  | 'ios-tab'
  | 'default'
  | 'denied'
  | 'granted'
  | 'subscribed'
  | 'failed';

@Injectable({ providedIn: 'root' })
export class PushNotificationService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly zone = inject(NgZone);
  private readonly router = inject(Router);
  private readonly api = inject(PushSubscriptionsApi);
  private readonly install = inject(InstallPromptService);
  private readonly swPush = inject(SwPush, { optional: true });

  private readonly permission = signal<NotificationPermission | 'unsupported'>('unsupported');
  private readonly subscribed = signal(false);
  private readonly failed = signal(false);
  private readonly promptDismissed = signal(false);
  private readonly deviceId = signal<string | null>(null);
  private syncStarted = false;

  readonly state = computed<PushUiState>(() => {
    if (this.isIosSafariTab()) {
      return 'ios-tab';
    }
    if (!this.isSupported()) {
      return 'unsupported';
    }
    if (this.failed()) {
      return 'failed';
    }
    if (this.subscribed()) {
      return 'subscribed';
    }
    const perm = this.permission();
    if (perm === 'denied') {
      return 'denied';
    }
    if (perm === 'granted') {
      return 'granted';
    }
    return 'default';
  });

  /** Soft prompt eligibility (caller must also require authenticated session). */
  readonly softPromptEligible = computed(() => {
    if (this.promptDismissed() || this.subscribed()) {
      return false;
    }
    if (this.isIosSafariTab() || !this.isPushContextRelevant()) {
      return false;
    }
    const s = this.state();
    return s === 'default' || s === 'granted' || s === 'failed';
  });

  constructor() {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    this.restoreDismiss();
    this.restoreDeviceId();
    this.refreshPermission();
    this.wireNotificationClicks();
  }

  /** Call once from the authenticated shell after login. */
  startLifecycle(): void {
    if (!isPlatformBrowser(this.platformId) || this.syncStarted) {
      return;
    }
    this.syncStarted = true;
    this.refreshPermission();
    void this.resyncIfGranted();
    this.wireSubscriptionChange();
  }

  isSupported(): boolean {
    if (!isPlatformBrowser(this.platformId)) {
      return false;
    }
    return !!this.swPush?.isEnabled && 'Notification' in this.document.defaultView!;
  }

  isIosSafariTab(): boolean {
    return this.install.isIos() && !this.install.isInstalled();
  }

  /** Installed PWA or Chromium desktop/Android — not iOS Safari tab. */
  isPushContextRelevant(): boolean {
    if (this.isIosSafariTab()) {
      return false;
    }
    if (this.install.isInstalled()) {
      return true;
    }
    return this.isChromiumLike();
  }

  dismissSoftPrompt(): void {
    this.promptDismissed.set(true);
    try {
      localStorage.setItem(PROMPT_DISMISS_KEY, '1');
    } catch {
      /* ignore */
    }
  }

  openIosInstallHelp(): void {
    this.install.openHowToInstall();
  }

  async enableFromUserGesture(): Promise<boolean> {
    this.failed.set(false);
    if (this.isIosSafariTab()) {
      this.openIosInstallHelp();
      return false;
    }
    if (!this.isSupported() || !this.swPush) {
      this.failed.set(true);
      return false;
    }

    try {
      const { publicKey } = await this.api.vapidPublicKey();
      const sub = await this.swPush.requestSubscription({ serverPublicKey: publicKey });
      const json = sub.toJSON();
      if (!json.endpoint || !json.keys?.['p256dh'] || !json.keys?.['auth']) {
        this.failed.set(true);
        return false;
      }
      const device = await this.api.upsert(
        json.endpoint,
        json.keys['p256dh'],
        json.keys['auth'],
        deviceLabelFromUa(this.document.defaultView?.navigator.userAgent),
      );
      this.deviceId.set(device.id);
      try {
        localStorage.setItem(DEVICE_ID_KEY, device.id);
      } catch {
        /* ignore */
      }
      this.subscribed.set(true);
      this.refreshPermission();
      return true;
    } catch {
      this.failed.set(true);
      this.refreshPermission();
      return false;
    }
  }

  async loadDevices(): Promise<Awaited<ReturnType<PushSubscriptionsApi['list']>>['items']> {
    const result = await this.api.list();
    return result.items;
  }

  async removeDevice(id: string): Promise<void> {
    await this.api.delete(id);
    if (this.deviceId() === id) {
      this.deviceId.set(null);
      this.subscribed.set(false);
      try {
        localStorage.removeItem(DEVICE_ID_KEY);
      } catch {
        /* ignore */
      }
      try {
        await this.swPush?.unsubscribe();
      } catch {
        /* ignore */
      }
    }
  }

  async sendTest(): Promise<void> {
    await this.api.sendTest();
  }

  /** Unsubscribe this device server-side and locally before clearing the session. */
  async prepareLogout(): Promise<void> {
    const id = this.deviceId();
    try {
      if (id) {
        await this.api.delete(id);
      }
    } catch {
      /* still clear local subscription */
    }
    try {
      await this.swPush?.unsubscribe();
    } catch {
      /* ignore */
    }
    this.subscribed.set(false);
    this.deviceId.set(null);
    this.syncStarted = false;
    try {
      localStorage.removeItem(DEVICE_ID_KEY);
    } catch {
      /* ignore */
    }
  }

  private async resyncIfGranted(): Promise<void> {
    this.refreshPermission();
    if (this.permission() !== 'granted' || !this.swPush?.isEnabled) {
      return;
    }
    try {
      const existing = await firstValueFrom(this.swPush.subscription);
      if (!existing) {
        return;
      }
      const json = existing.toJSON();
      if (!json.endpoint || !json.keys?.['p256dh'] || !json.keys?.['auth']) {
        return;
      }
      const device = await this.api.upsert(
        json.endpoint,
        json.keys['p256dh'],
        json.keys['auth'],
        deviceLabelFromUa(this.document.defaultView?.navigator.userAgent),
      );
      this.deviceId.set(device.id);
      try {
        localStorage.setItem(DEVICE_ID_KEY, device.id);
      } catch {
        /* ignore */
      }
      this.subscribed.set(true);
    } catch {
      /* vapid missing or network — leave UI as granted */
    }
  }

  private wireSubscriptionChange(): void {
    const nav = this.document.defaultView?.navigator;
    if (!nav?.serviceWorker) {
      return;
    }
    nav.serviceWorker.addEventListener('message', (event) => {
      const data = (event as MessageEvent).data as { type?: string } | undefined;
      if (data?.type === 'PUSH_SUBSCRIPTION_CHANGE') {
        this.zone.run(() => void this.resyncIfGranted());
      }
    });
  }

  private wireNotificationClicks(): void {
    if (!this.swPush?.isEnabled) {
      return;
    }
    this.swPush.notificationClicks.subscribe((event) => {
      this.zone.run(() => {
        const raw =
          (event.notification?.data as { url?: string } | undefined)?.url ??
          (event.notification?.data as { onActionClick?: { default?: { url?: string } } } | undefined)
            ?.onActionClick?.default?.url;
        const path = resolveNotificationDeepLink(raw);
        void this.router.navigateByUrl(path);
      });
    });
  }

  private refreshPermission(): void {
    if (!isPlatformBrowser(this.platformId) || !('Notification' in this.document.defaultView!)) {
      this.permission.set('unsupported');
      return;
    }
    this.permission.set(Notification.permission);
  }

  private isChromiumLike(): boolean {
    const ua = this.document.defaultView?.navigator.userAgent ?? '';
    return /Chrome|Chromium|Edg|CriOS|Android/i.test(ua) && !/Firefox/i.test(ua);
  }

  private restoreDismiss(): void {
    try {
      this.promptDismissed.set(localStorage.getItem(PROMPT_DISMISS_KEY) === '1');
    } catch {
      /* ignore */
    }
  }

  private restoreDeviceId(): void {
    try {
      this.deviceId.set(localStorage.getItem(DEVICE_ID_KEY));
    } catch {
      /* ignore */
    }
  }
}

function deviceLabelFromUa(ua: string | undefined): string {
  if (!ua) {
    return 'Browser';
  }
  if (/Edg\//i.test(ua)) {
    return 'Edge';
  }
  if (/Chrome\//i.test(ua) && /Android/i.test(ua)) {
    return 'Chrome Android';
  }
  if (/Chrome\//i.test(ua)) {
    return 'Chrome';
  }
  if (/Safari\//i.test(ua) && /iPhone|iPad/i.test(ua)) {
    return 'Safari iOS';
  }
  if (/Firefox\//i.test(ua)) {
    return 'Firefox';
  }
  return ua.slice(0, 48);
}
