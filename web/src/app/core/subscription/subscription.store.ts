import { computed, inject, Injectable, signal } from '@angular/core';
import { MeSubscriptionSummary } from '../api/subscription.models';
import { SubscriptionApi } from '../api/subscription.api';
import { FeatureKey } from './feature-keys';

const DISMISS_KEY = 'kadr.trialBannerDismissedDate';

@Injectable({ providedIn: 'root' })
export class SubscriptionStore {
  private readonly api = inject(SubscriptionApi);

  private readonly summarySignal = signal<MeSubscriptionSummary | null>(null);
  private readonly seatDialogOpenSignal = signal(false);
  private readonly seatCapSignal = signal<number | null>(null);
  private readonly seatActiveSignal = signal(0);

  readonly summary = this.summarySignal.asReadonly();
  readonly readOnly = computed(() => this.summarySignal()?.readOnly ?? false);
  readonly features = computed(() => this.summarySignal()?.features ?? {});
  readonly daysLeft = computed(() => this.summarySignal()?.daysLeft ?? null);
  readonly state = computed(() => this.summarySignal()?.state ?? null);
  readonly seatDialogOpen = this.seatDialogOpenSignal.asReadonly();
  readonly seatCap = this.seatCapSignal.asReadonly();
  readonly seatActive = this.seatActiveSignal.asReadonly();

  readonly bannerKind = computed<'none' | 'trialInfo' | 'trialWarn' | 'readOnly'>(() => {
    const s = this.summarySignal();
    if (!s) {
      return 'none';
    }
    if (s.readOnly) {
      return 'readOnly';
    }
    if (s.state === 'trialRunning') {
      const days = s.daysLeft ?? 0;
      if (days <= 3) {
        return 'trialWarn';
      }
      if (this.isTrialInfoDismissedToday()) {
        return 'none';
      }
      return 'trialInfo';
    }
    return 'none';
  });

  applyFromMe(subscription: MeSubscriptionSummary | null | undefined): void {
    this.summarySignal.set(subscription ?? null);
  }

  clear(): void {
    this.summarySignal.set(null);
    this.seatDialogOpenSignal.set(false);
  }

  hasFeature(key: FeatureKey | string): boolean {
    return !!this.features()[key];
  }

  dismissTrialInfoBanner(): void {
    const today = new Date().toISOString().slice(0, 10);
    try {
      localStorage.setItem(DISMISS_KEY, today);
    } catch {
      // ignore quota / private mode
    }
    // Recompute by rewriting summary (bannerKind reads localStorage).
    this.summarySignal.update((s) => (s ? { ...s } : s));
  }

  openSeatLimitDialog(cap: number | null, active: number): void {
    this.seatCapSignal.set(cap);
    this.seatActiveSignal.set(active);
    this.seatDialogOpenSignal.set(true);
  }

  closeSeatLimitDialog(): void {
    this.seatDialogOpenSignal.set(false);
  }

  async refreshFromApi(): Promise<void> {
    const dto = await this.api.getSubscription();
    this.summarySignal.set({
      state: dto.effectiveState,
      trialEndsAt: dto.trialEndsAt,
      daysLeft: dto.daysLeft,
      readOnly: dto.readOnly,
      features: dto.features,
    });
  }

  private isTrialInfoDismissedToday(): boolean {
    try {
      return localStorage.getItem(DISMISS_KEY) === new Date().toISOString().slice(0, 10);
    } catch {
      return false;
    }
  }
}
