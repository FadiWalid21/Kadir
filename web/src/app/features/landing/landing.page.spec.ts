import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PublicPlansApi } from '../../core/api/public-plans.api';
import { ThemeService } from '../../core/theme/theme.service';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { LandingPage } from './landing.page';

describe('LandingPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LandingPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: PublicPlansApi,
          useValue: {
            list: vi.fn().mockResolvedValue([
              {
                code: 'starter',
                nameAr: 'المبتدئ',
                nameEn: 'Starter',
                pricingModel: 'flatTier',
                currency: 'EGP',
                unitPrice: 1499,
                employeeCap: 25,
                minBilledEmployees: null,
                features: {},
              },
            ]),
          },
        },
        {
          provide: ThemeService,
          useValue: {
            language: signal('ar'),
            direction: signal('rtl'),
            resolvedTheme: signal('light'),
            setLanguage: vi.fn(),
            setTheme: vi.fn(),
          },
        },
      ],
    }).compileComponents();

    const i18n = TestBed.inject(TranslocoService);
    await new Promise<void>((resolve) => i18n.load('ar').subscribe(() => resolve()));
  });

  it('renders translated copy without raw landing.* keys', async () => {
    const fixture = TestBed.createComponent(LandingPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toMatch(/\blanding\.[a-zA-Z0-9_.]+/);
    expect(text).toContain('حضور وإجازة وفريق');
    expect(text).not.toContain('قريبًا');
    expect(text).not.toContain('بيانات تجريبية');
  });

  it('shows Western digits and localized currency symbol for plan prices', async () => {
    const fixture = TestBed.createComponent(LandingPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toMatch(/1[,.]?499/);
    expect(text).toContain('ج.م');
    expect(text).not.toContain('الفوترة بعملة');
  });
});

