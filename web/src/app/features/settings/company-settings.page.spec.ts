import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TenantApi } from '../../core/api/tenant.api';
import { AuthService } from '../../core/auth/auth.service';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { CompanySettingsPage } from './company-settings.page';

describe('CompanySettingsPage', () => {
  const readOnly = signal(false);

  beforeEach(async () => {
    readOnly.set(false);
    await TestBed.configureTestingModule({
      imports: [CompanySettingsPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: TenantApi,
          useValue: {
            getSettings: vi.fn().mockResolvedValue({
              name: 'شركة الاختبار',
              defaultLanguage: 'ar',
              timeZone: 'Africa/Cairo',
              logoFileId: null,
              logoUrl: null,
            }),
            updateSettings: vi.fn(),
            uploadLogo: vi.fn(),
            deleteLogo: vi.fn(),
          },
        },
        {
          provide: AuthService,
          useValue: {
            me: signal({
              id: 'u1',
              fullName: 'مالك',
              email: 'o@example.com',
              language: 'ar',
              roles: ['Owner'],
              permissions: ['tenant.settings.read', 'tenant.settings.update'],
              tenant: { id: 't1', name: 'شركة الاختبار', slug: 'test', logoUrl: null },
              checklist: { inviteTeamDone: false },
            }),
            hasPermission: () => true,
            refreshMe: vi.fn().mockResolvedValue(undefined),
          },
        },
        {
          provide: SubscriptionStore,
          useValue: {
            readOnly,
          },
        },
      ],
    }).compileComponents();
  });

  it('renders company branding form in Arabic', async () => {
    const fixture = TestBed.createComponent(CompanySettingsPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('إعدادات الشركة');
    expect(text).toContain('الهوية البصرية للمنشأة');
    expect(text).toContain('حفظ التغييرات');
    expect(fixture.nativeElement.querySelector('kadr-tenant-logo')).toBeTruthy();
  });

  it('hides primary save when subscription is read-only', async () => {
    readOnly.set(true);
    const fixture = TestBed.createComponent(CompanySettingsPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('حفظ التغييرات');
  });
});
