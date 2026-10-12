import { DOCUMENT } from '@angular/common';
import { Injectable, NgZone, computed, inject, signal } from '@angular/core';

const DISMISS_KEY = 'kadr.pwa.installBannerDismissed';

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

@Injectable({ providedIn: 'root' })
export class InstallPromptService {
  private readonly document = inject(DOCUMENT);
  private readonly zone = inject(NgZone);

  private deferred: BeforeInstallPromptEvent | null = null;
  private readonly promptAvailable = signal(false);
  private readonly installedSignal = signal(false);
  private readonly dismissed = signal(false);
  private readonly standalone = signal(false);

  readonly isIos = signal(false);
  readonly showHowTo = signal(false);

  /** Native Chromium install prompt is available. */
  readonly canInstall = computed(
    () => this.promptAvailable() && !this.isInstalled(),
  );

  /** Running as installed PWA (standalone) or after appinstalled. */
  readonly isInstalled = computed(() => this.standalone() || this.installedSignal());

  /** Slim top banner: installable (or iOS Safari) and not dismissed. */
  readonly showBanner = computed(() => {
    if (this.isInstalled()) {
      return false;
    }
    if (this.dismissed()) {
      return false;
    }
    return this.canInstall() || this.isIos();
  });

  /** Avatar menu: ignore dismiss; show when installable or iOS not standalone. */
  readonly showMenuItem = computed(() => {
    if (this.isInstalled()) {
      return false;
    }
    return this.canInstall() || this.isIos();
  });

  constructor() {
    this.restoreDismiss();
    this.refreshStandalone();
    this.isIos.set(this.detectIosSafari());

    if (typeof window === 'undefined') {
      return;
    }

    window.addEventListener('beforeinstallprompt', (event) => {
      event.preventDefault();
      this.zone.run(() => {
        this.deferred = event as BeforeInstallPromptEvent;
        this.promptAvailable.set(true);
      });
    });

    window.addEventListener('appinstalled', () => {
      this.zone.run(() => {
        this.deferred = null;
        this.promptAvailable.set(false);
        this.installedSignal.set(true);
        this.showHowTo.set(false);
      });
    });

    if (typeof window.matchMedia === 'function') {
      const mq = window.matchMedia('(display-mode: standalone)');
      mq.addEventListener('change', () => this.zone.run(() => this.refreshStandalone()));
    }
  }

  async promptInstall(): Promise<boolean> {
    if (this.isIos() && !this.canInstall()) {
      this.showHowTo.set(true);
      return false;
    }
    const event = this.deferred;
    if (!event) {
      return false;
    }
    await event.prompt();
    const choice = await event.userChoice;
    this.deferred = null;
    this.promptAvailable.set(false);
    return choice.outcome === 'accepted';
  }

  openHowToInstall(): void {
    if (this.isIos() && !this.isInstalled()) {
      this.showHowTo.set(true);
    }
  }

  closeHowTo(): void {
    this.showHowTo.set(false);
  }

  dismissBanner(): void {
    this.dismissed.set(true);
    this.showHowTo.set(false);
    try {
      localStorage.setItem(DISMISS_KEY, '1');
    } catch {
      /* ignore quota / private mode */
    }
  }

  private refreshStandalone(): void {
    const nav = this.document.defaultView?.navigator as Navigator & { standalone?: boolean };
    const displayStandalone =
      typeof matchMedia === 'function' && matchMedia('(display-mode: standalone)').matches;
    this.standalone.set(!!displayStandalone || !!nav?.standalone);
  }

  private detectIosSafari(): boolean {
    const ua = this.document.defaultView?.navigator.userAgent ?? '';
    const iOS =
      /iPad|iPhone|iPod/.test(ua) ||
      (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    const webkit = /WebKit/i.test(ua);
    const notOther = !/CriOS|FxiOS|EdgiOS|OPiOS|Chrome|Firefox|Edg/i.test(ua);
    return iOS && webkit && notOther;
  }

  private restoreDismiss(): void {
    try {
      this.dismissed.set(localStorage.getItem(DISMISS_KEY) === '1');
    } catch {
      /* ignore */
    }
  }
}
