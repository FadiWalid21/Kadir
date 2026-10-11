import { DOCUMENT } from '@angular/common';
import { Injectable, NgZone, inject, signal, computed } from '@angular/core';

const DISMISS_KEY = 'kadr.pwa.installDismissedAt';
const IOS_NEVER_KEY = 'kadr.pwa.iosInstallNever';
const DISMISS_MS = 14 * 24 * 60 * 60 * 1000;

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

@Injectable({ providedIn: 'root' })
export class InstallService {
  private readonly document = inject(DOCUMENT);
  private readonly zone = inject(NgZone);

  private deferred: BeforeInstallPromptEvent | null = null;
  private readonly promptAvailable = signal(false);
  private readonly installedSignal = signal(false);
  private readonly dismissedUntil = signal(0);
  private readonly iosNever = signal(false);

  readonly isStandalone = signal(false);
  readonly isIosSafari = signal(false);

  /** Chrome/Edge/Android native install is available. */
  readonly canInstall = computed(
    () => this.promptAvailable() && !this.isStandalone() && !this.installedSignal(),
  );

  /** Show Home install card (BIP path or iOS sheet path), respecting 14-day dismiss. */
  readonly showHomeCard = computed(() => {
    if (this.isStandalone() || this.installedSignal()) {
      return false;
    }
    if (Date.now() < this.dismissedUntil()) {
      return false;
    }
    if (this.canInstall()) {
      return true;
    }
    return this.isIosSafari() && !this.iosNever();
  });

  /** Permanent menu item when install is possible (BIP or iOS instructions). */
  readonly showMenuItem = computed(() => {
    if (this.isStandalone() || this.installedSignal()) {
      return false;
    }
    return this.canInstall() || (this.isIosSafari() && !this.iosNever());
  });

  readonly showIosSheet = signal(false);

  constructor() {
    this.restorePrefs();
    this.refreshStandalone();
    this.isIosSafari.set(this.detectIosSafari());

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
        this.showIosSheet.set(false);
      });
    });

    if (typeof window.matchMedia === 'function') {
      const mq = window.matchMedia('(display-mode: standalone)');
      mq.addEventListener('change', () => this.zone.run(() => this.refreshStandalone()));
    }
  }

  async install(): Promise<boolean> {
    if (this.isIosSafari() && !this.canInstall()) {
      this.showIosSheet.set(true);
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

  openIosSheet(): void {
    if (this.isIosSafari() && !this.isStandalone()) {
      this.showIosSheet.set(true);
    }
  }

  closeIosSheet(): void {
    this.showIosSheet.set(false);
  }

  dismissHomeCard(): void {
    const until = Date.now() + DISMISS_MS;
    this.dismissedUntil.set(until);
    try {
      localStorage.setItem(DISMISS_KEY, String(Date.now()));
    } catch {
      /* ignore */
    }
  }

  neverShowIosSheet(): void {
    this.iosNever.set(true);
    this.showIosSheet.set(false);
    try {
      localStorage.setItem(IOS_NEVER_KEY, '1');
    } catch {
      /* ignore */
    }
  }

  private refreshStandalone(): void {
    const nav = this.document.defaultView?.navigator as Navigator & { standalone?: boolean };
    const displayStandalone =
      typeof matchMedia === 'function' && matchMedia('(display-mode: standalone)').matches;
    this.isStandalone.set(!!displayStandalone || !!nav?.standalone);
  }

  private detectIosSafari(): boolean {
    const ua = this.document.defaultView?.navigator.userAgent ?? '';
    const iOS = /iPad|iPhone|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    const webkit = /WebKit/i.test(ua);
    const notOther = !/CriOS|FxiOS|EdgiOS|OPiOS|Chrome|Firefox|Edg/i.test(ua);
    return iOS && webkit && notOther;
  }

  private restorePrefs(): void {
    try {
      const raw = localStorage.getItem(DISMISS_KEY);
      if (raw) {
        const at = Number(raw);
        if (!Number.isNaN(at)) {
          this.dismissedUntil.set(at + DISMISS_MS);
        }
      }
      this.iosNever.set(localStorage.getItem(IOS_NEVER_KEY) === '1');
    } catch {
      /* ignore */
    }
  }
}
