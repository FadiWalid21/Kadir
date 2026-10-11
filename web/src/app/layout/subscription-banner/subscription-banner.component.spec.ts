import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { SubscriptionBannerComponent } from './subscription-banner.component';

describe('SubscriptionBannerComponent', () => {
  const bannerKind = signal<'none' | 'trialInfo' | 'trialWarn' | 'readOnly'>('none');
  const daysLeft = signal<number | null>(10);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubscriptionBannerComponent],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        {
          provide: SubscriptionStore,
          useValue: {
            bannerKind,
            daysLeft,
            dismissTrialInfoBanner: () => undefined,
          },
        },
      ],
    }).compileComponents();
  });

  it('renders trial info banner', () => {
    bannerKind.set('trialInfo');
    const fixture = TestBed.createComponent(SubscriptionBannerComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('تجربتك');
  });

  it('renders amber warn banner', () => {
    bannerKind.set('trialWarn');
    daysLeft.set(2);
    const fixture = TestBed.createComponent(SubscriptionBannerComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('تنتهي تجربتك');
  });

  it('renders read-only banner', () => {
    bannerKind.set('readOnly');
    const fixture = TestBed.createComponent(SubscriptionBannerComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('العرض والتصدير فقط');
  });
});
