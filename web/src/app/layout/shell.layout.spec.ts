import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../core/auth/auth.service';
import { provideTestTransloco } from '../testing/transloco-testing';
import { ShellLayout } from './shell.layout';

describe('ShellLayout', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ShellLayout],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            me: signal({
              id: 'u1',
              fullName: 'مالك الاختبار',
              email: 'owner@example.com',
              language: 'ar',
              roles: ['Owner'],
              permissions: ['users.read', 'tenant.settings.read'],
              tenant: { id: 't1', name: 'شركة الاختبار', slug: 'test-co', logoUrl: null },
              checklist: { inviteTeamDone: false },
              subscription: {
                state: 'trialRunning',
                trialEndsAt: null,
                daysLeft: 10,
                readOnly: false,
                features: {},
              },
            }),
            status: signal('authenticated'),
            accessToken: signal('test-token'),
            hasPermission: (code: string) =>
              ['users.read', 'tenant.settings.read'].includes(code),
            logout: vi.fn(),
            restoreSession: vi.fn().mockResolvedValue(undefined),
          },
        },
      ],
    }).compileComponents();
  });

  it('renders branded shell with company name in Arabic', async () => {
    const fixture = TestBed.createComponent(ShellLayout);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('kadr-top-bar')).toBeTruthy();
    expect(el.querySelector('kadr-sidebar')).toBeTruthy();
    expect(el.querySelector('kadr-bottom-nav')).toBeTruthy();
    expect(el.textContent).toContain('شركة الاختبار');
    expect(el.textContent).toContain('الرئيسية');
  });
});
