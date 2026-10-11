import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SubscriptionApi } from '../../core/api/subscription.api';
import { AuthService } from '../../core/auth/auth.service';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ThemeService } from '../../core/theme/theme.service';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { SubscriptionSettingsPage } from './subscription-settings.page';

describe('SubscriptionSettingsPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubscriptionSettingsPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        provideHttpClient(),
        provideHttpClientTesting(),
        SubscriptionStore,
        {
          provide: ThemeService,
          useValue: {
            language: signal('ar'),
          },
        },
        {
          provide: AuthService,
          useValue: {
            hasPermission: () => true,
            me: signal(null),
            status: signal('authenticated'),
          },
        },
        {
          provide: SubscriptionApi,
          useValue: {
            getSubscription: vi.fn().mockResolvedValue({
              plan: {
                code: 'starter',
                nameAr: 'المبتدئ الطويل جداً للاختبار',
                nameEn: 'Starter',
                pricingModel: 'flatTier',
                employeeCap: 25,
                unitPrice: 1499,
                minBilledEmployees: null,
                currency: 'EGP',
              },
              status: 'trialing',
              effectiveState: 'trialRunning',
              readOnly: false,
              startsAt: new Date().toISOString(),
              trialStartsAt: new Date().toISOString(),
              trialEndsAt: new Date(Date.now() + 86400000 * 10).toISOString(),
              daysLeft: 10,
              usage: { activeEmployees: 0, cap: 25 },
              features: { core_hr: true, attendance: true, leave: true, ess: true },
            }),
            listPlans: vi.fn().mockResolvedValue([
              {
                code: 'starter',
                nameAr: 'المبتدئ',
                nameEn: 'Starter',
                pricingModel: 'flatTier',
                employeeCap: 25,
                unitPrice: 1499,
                minBilledEmployees: null,
                currency: 'EGP',
                features: { core_hr: true },
              },
            ]),
          },
        },
      ],
    }).compileComponents();
  });

  it('renders subscription page in Arabic with long plan name', async () => {
    const fixture = TestBed.createComponent(SubscriptionSettingsPage);
    fixture.detectChanges();
    await vi.waitFor(() => {
      expect((fixture.componentInstance as unknown as { state: () => string }).state()).toBe(
        'ready',
      );
    });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('الاشتراك');
    expect(text).toContain('المبتدئ الطويل جداً للاختبار');
    expect(text).toContain('تواصل معنا');
  });
});
