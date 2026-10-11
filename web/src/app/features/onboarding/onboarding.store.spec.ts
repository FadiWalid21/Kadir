import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApi } from '../../core/api/auth.api';
import { TemplatesApi } from '../../core/api/templates.api';
import { ONBOARDING_DRAFT_KEY, OnboardingStore } from './onboarding.store';

describe('OnboardingStore', () => {
  let store: OnboardingStore;
  let authApi: { register: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    sessionStorage.clear();
    authApi = {
      register: vi.fn().mockResolvedValue({
        tenantId: 't1',
        slug: 'acme',
        trialEndsAt: '2026-11-01T00:00:00Z',
      }),
    };
    TestBed.configureTestingModule({
      providers: [
        OnboardingStore,
        { provide: AuthApi, useValue: authApi },
        {
          provide: TemplatesApi,
          useValue: {
            listIndustries: vi.fn().mockResolvedValue([
              { code: 'office', nameAr: 'مكاتب', nameEn: 'Office' },
            ]),
          },
        },
      ],
    });
    store = TestBed.inject(OnboardingStore);
  });

  it('persists draft to sessionStorage and hydrates', () => {
    store.patch({ fullName: 'مالك', companyName: 'شركة طويلة جداً للاختبار' });
    const raw = sessionStorage.getItem(ONBOARDING_DRAFT_KEY);
    expect(raw).toBeTruthy();

    const again = TestBed.inject(OnboardingStore);
    again.hydrate();
    expect(again.draft().fullName).toBe('مالك');
    expect(again.draft().companyName).toContain('شركة');
  });

  it('submit sends Idempotency-Key and clears draft on success', async () => {
    store.patch({
      fullName: 'Owner',
      email: 'o@example.com',
      password: 'Str0ngPass!!',
      companyName: 'Acme',
      industryCode: 'office',
      industryConfirmed: true,
      firstBranchName: 'HQ',
    });

    const ok = await store.submit();
    expect(ok).toBe(true);
    expect(authApi.register).toHaveBeenCalledOnce();
    const [, key] = authApi.register.mock.calls[0]!;
    expect(typeof key).toBe('string');
    expect(key.length).toBeGreaterThan(8);
    expect(sessionStorage.getItem(ONBOARDING_DRAFT_KEY)).toBeNull();
    expect(store.draft().result?.slug).toBe('acme');
  });

  it('maps register body fields from draft', () => {
    store.patch({
      fullName: ' Owner ',
      email: ' o@x.com ',
      password: 'Str0ngPass!!',
      companyName: ' Co ',
      industryCode: 'retail',
      workWeekDays: ['sun', 'mon'],
      workWeekWeekend: ['fri'],
      defaultWorkStart: '08:00',
      defaultWorkEnd: '16:00',
      firstBranchName: ' Branch ',
    });
    const body = store.toRegisterBody();
    expect(body.fullName).toBe('Owner');
    expect(body.industryCode).toBe('retail');
    expect(body.workWeekDays).toEqual(['sun', 'mon']);
    expect(body.firstBranchName).toBe('Branch');
  });

  describe('Google registration', () => {
    it('keeps the registration token in memory only (never sessionStorage)', () => {
      store.startGoogleRegistration({
        token: 'reg-secret-token',
        email: 'g@example.com',
        fullName: 'Google User',
      });
      expect(store.draft().googleVerified).toBe(true);
      expect(store.draft().googleRegistrationToken).toBe('reg-secret-token');
      expect(store.draft().email).toBe('g@example.com');
      const raw = sessionStorage.getItem(ONBOARDING_DRAFT_KEY) ?? '';
      expect(raw).not.toContain('reg-secret-token');
      expect(raw).toContain('g@example.com');
    });

    it('register body sends the token and omits email; password is optional', () => {
      store.startGoogleRegistration({
        token: 'reg-1',
        email: 'g@example.com',
        fullName: 'G',
      });
      store.patch({
        companyName: 'Co',
        industryCode: 'office',
        industryConfirmed: true,
        firstBranchName: 'HQ',
      });
      expect(store.toRegisterBody()).toMatchObject({
        fullName: 'G',
        googleRegistrationToken: 'reg-1',
        companyName: 'Co',
      });
      expect(store.toRegisterBody().email).toBeUndefined();
      expect(store.toRegisterBody().password).toBeUndefined();

      store.patch({ password: 'Str0ngPass!!', passwordConfirm: 'Str0ngPass!!' });
      expect(store.toRegisterBody().password).toBe('Str0ngPass!!');
      expect(store.validateCurrentStep()).toBeNull();
    });

    it('marks Google confirmation expired and blocks submit', () => {
      store.startGoogleRegistration({
        token: 'reg-1',
        email: 'g@example.com',
        fullName: 'G',
      });
      store.patch({ googleRegisteredAt: Date.now() - 31 * 60 * 1000 });
      expect(store.checkGoogleExpiry()).toBe(true);
      expect(store.draft().googleRegistrationToken).toBeNull();
      expect(store.draft().stepIndex).toBe(0);
      expect(store.validateCurrentStep()).toBe('onboarding.errors.googleExpired');
    });

    it('discard returns step 1 to the normal email/password form', () => {
      store.startGoogleRegistration({
        token: 'reg-1',
        email: 'g@example.com',
        fullName: 'G',
      });
      store.discardGoogleRegistration();
      expect(store.draft().googleVerified).toBe(false);
      expect(store.draft().googleRegistrationToken).toBeNull();
      expect(store.draft().email).toBe('');
    });
  });
});
