import { DOCUMENT } from '@angular/common';
import { Injectable, NgZone, inject, signal } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter } from 'rxjs';
import { DirtyFormRegistry } from '../forms/dirty-form.registry';

export type UpdateNoticeState = 'hidden' | 'ready' | 'updating';

/** Check often enough that a Vercel deploy is noticed within a few minutes. */
const CHECK_INTERVAL_MS = 5 * 60 * 1000;

@Injectable({ providedIn: 'root' })
export class UpdateService {
  private readonly updates = inject(SwUpdate, { optional: true });
  private readonly dirty = inject(DirtyFormRegistry);
  private readonly document = inject(DOCUMENT);
  private readonly zone = inject(NgZone);

  private readonly stateSignal = signal<UpdateNoticeState>('hidden');
  private readonly dismissedSignal = signal(false);
  private readonly onVisibility = (): void => {
    if (this.document.visibilityState === 'visible') {
      void this.checkForUpdate();
    }
  };
  private readonly onOnline = (): void => {
    void this.checkForUpdate();
  };

  readonly state = this.stateSignal.asReadonly();
  readonly hasUnsavedChanges = this.dirty.hasDirty;

  constructor() {
    if (!this.updates?.isEnabled) {
      return;
    }

    this.updates.versionUpdates
      .pipe(filter((e): e is VersionReadyEvent => e.type === 'VERSION_READY'))
      .subscribe(() => {
        this.zone.run(() => {
          this.dismissedSignal.set(false);
          this.stateSignal.set('ready');
        });
      });

    void this.checkForUpdate();
    this.zone.runOutsideAngular(() => {
      window.setInterval(() => void this.checkForUpdate(), CHECK_INTERVAL_MS);
    });

    this.document.addEventListener('visibilitychange', this.onVisibility);
    this.document.defaultView?.addEventListener('online', this.onOnline);
  }

  dismiss(): void {
    this.dismissedSignal.set(true);
    this.stateSignal.set('hidden');
  }

  /** Ui-kit / tests: show the ready notice without a service-worker event. */
  previewReady(): void {
    this.dismissedSignal.set(false);
    this.stateSignal.set('ready');
  }

  /** Visible when a version is ready (or updating) and the user has not dismissed. */
  shouldShow(): boolean {
    if (this.dismissedSignal() && this.stateSignal() === 'hidden') {
      return false;
    }
    return this.stateSignal() === 'ready' || this.stateSignal() === 'updating';
  }

  async applyUpdate(): Promise<void> {
    if (!this.updates?.isEnabled || this.stateSignal() === 'updating') {
      return;
    }
    this.stateSignal.set('updating');
    try {
      await this.updates.activateUpdate();
      // Never automatic: only after the user chose Update now.
      this.document.defaultView?.location.reload();
    } catch {
      this.stateSignal.set('ready');
    }
  }

  /** Test hook: simulate a visibility restore. */
  handleVisibilityForTests(): void {
    this.onVisibility();
  }

  /** Test hook: simulate coming back online. */
  handleOnlineForTests(): void {
    this.onOnline();
  }

  private async checkForUpdate(): Promise<void> {
    if (!this.updates?.isEnabled) {
      return;
    }
    try {
      await this.updates.checkForUpdate();
    } catch {
      /* offline or SW unavailable */
    }
  }
}
