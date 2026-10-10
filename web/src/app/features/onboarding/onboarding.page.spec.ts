import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { OnboardingPage } from './onboarding.page';
import { OnboardingStore } from './onboarding.store';

describe('OnboardingPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OnboardingPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        {
          provide: OnboardingStore,
          useValue: {
            draft: signal({
              stepIndex: 2,
              fullName: '',
              email: '',
              password: '',
              companyName: '',
              countryCode: 'EG',
              timeZone: 'Africa/Cairo',
              defaultLanguage: 'ar',
              industryCode: 'office',
              industryConfirmed: false,
              workWeekDays: ['sun'],
              workWeekWeekend: ['fri'],
              defaultWorkStart: '09:00',
              defaultWorkEnd: '17:00',
              firstBranchName: '',
              idempotencyKey: null,
              result: null,
            }),
            industries: signal([
              { code: 'office', nameAr: 'المكاتب والخدمات', nameEn: 'Office & Services' },
              { code: 'retail', nameAr: 'التجزئة والمتاجر', nameEn: 'Retail & Stores' },
            ]),
            submitting: signal(false),
            error: signal(null),
            hydrate: vi.fn(),
            loadIndustries: vi.fn().mockResolvedValue(undefined),
            patch: vi.fn(),
            setStepIndex: vi.fn(),
            validateCurrentStep: vi.fn().mockReturnValue(null),
            validateStep: vi.fn().mockReturnValue(null),
            firstInvalidStepIndex: vi.fn().mockReturnValue(null),
            submit: vi.fn(),
          },
        },
      ],
    }).compileComponents();
  });

  it('renders stepper and industry step in Arabic', async () => {
    const fixture = TestBed.createComponent(OnboardingPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const text = el.textContent ?? '';
    expect(text).toContain('إنشاء شركتك على كادر');
    expect(text).toContain('القطاع');
    expect(text).toContain('المكاتب والخدمات');
    expect(el.querySelector('kadr-stepper')).toBeTruthy();
    expect(el.querySelector('[data-testid="auth-shell-card"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="auth-brand-panel"]')).toBeTruthy();
  });
});
