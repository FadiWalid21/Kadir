import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SubscriptionApi } from '../api/subscription.api';
import { SubscriptionStore } from './subscription.store';

describe('SubscriptionStore', () => {
  let store: SubscriptionStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SubscriptionStore,
        {
          provide: SubscriptionApi,
          useValue: {
            getSubscription: vi.fn(),
            listPlans: vi.fn(),
          },
        },
      ],
    });
    store = TestBed.inject(SubscriptionStore);
    localStorage.clear();
  });

  it('exposes readOnly and features from me summary', () => {
    store.applyFromMe({
      state: 'trialRunning',
      trialEndsAt: new Date().toISOString(),
      daysLeft: 10,
      readOnly: false,
      features: { core_hr: true, shifts: false },
    });
    expect(store.readOnly()).toBe(false);
    expect(store.hasFeature('core_hr')).toBe(true);
    expect(store.hasFeature('shifts')).toBe(false);
    expect(store.bannerKind()).toBe('trialInfo');
  });

  it('shows amber banner when daysLeft <= 3', () => {
    store.applyFromMe({
      state: 'trialRunning',
      trialEndsAt: new Date().toISOString(),
      daysLeft: 2,
      readOnly: false,
      features: {},
    });
    expect(store.bannerKind()).toBe('trialWarn');
  });

  it('shows non-dismissible read-only banner when expired', () => {
    store.applyFromMe({
      state: 'trialExpired',
      trialEndsAt: new Date().toISOString(),
      daysLeft: 0,
      readOnly: true,
      features: {},
    });
    expect(store.bannerKind()).toBe('readOnly');
  });

  it('dismisses trial info until next day', () => {
    store.applyFromMe({
      state: 'trialRunning',
      trialEndsAt: new Date().toISOString(),
      daysLeft: 10,
      readOnly: false,
      features: {},
    });
    store.dismissTrialInfoBanner();
    expect(store.bannerKind()).toBe('none');
  });
});
