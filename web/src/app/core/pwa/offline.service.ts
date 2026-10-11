import { Injectable, NgZone, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { ToastService } from '../../shared/ui/toast/toast.service';

@Injectable({ providedIn: 'root' })
export class OfflineService {
  private readonly zone = inject(NgZone);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  private readonly offlineSignal = signal(
    typeof navigator !== 'undefined' ? !navigator.onLine : false,
  );

  readonly offline = this.offlineSignal.asReadonly();

  constructor() {
    if (typeof window === 'undefined') {
      return;
    }
    window.addEventListener('online', () => this.zone.run(() => this.setOffline(false)));
    window.addEventListener('offline', () => this.zone.run(() => this.setOffline(true)));
  }

  /** HTTP status 0 (network failure) marks the app offline. */
  noteFailedRequest(): void {
    this.setOffline(true);
  }

  /** Any successful HTTP response clears the offline flag. */
  noteSuccessfulRequest(): void {
    this.setOffline(false);
  }

  /** Desktop banner "recheck" — sync with navigator.onLine. */
  recheck(): void {
    this.setOffline(typeof navigator !== 'undefined' ? !navigator.onLine : false);
  }

  private setOffline(value: boolean): void {
    const was = this.offlineSignal();
    if (was === value) {
      return;
    }
    this.offlineSignal.set(value);
    if (was && !value) {
      this.toast.show(this.transloco.translate('pwa.offline.restored'), 'success');
    }
  }
}
