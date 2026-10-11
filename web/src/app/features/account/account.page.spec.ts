import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApi } from '../../core/api/auth.api';
import { MeApi } from '../../core/api/me.api';
import { AuthProvidersService } from '../../core/auth/auth-providers.service';
import { AuthService } from '../../core/auth/auth.service';
import { GoogleGisLoader } from '../../core/auth/google-gis.loader';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { AccountPage } from './account.page';

describe('AccountPage', () => {
  const googleEnabled = signal(true);
  let getSignInMethods: ReturnType<typeof vi.fn>;
  let unlinkGoogle: ReturnType<typeof vi.fn>;
  let linkGoogle: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    googleEnabled.set(true);
    getSignInMethods = vi.fn().mockResolvedValue({
      password: { enabled: true },
      google: { linked: true, email: 'g@example.com' },
    });
    unlinkGoogle = vi.fn().mockResolvedValue(undefined);
    linkGoogle = vi.fn().mockResolvedValue(undefined);

    await TestBed.configureTestingModule({
      imports: [AccountPage],
      providers: [
        provideTestTransloco(),
        {
          provide: MeApi,
          useValue: { getSignInMethods, linkGoogle, unlinkGoogle, setPassword: vi.fn() },
        },
        {
          provide: AuthService,
          useValue: { me: signal({ email: 'owner@example.com', fullName: 'Owner' }) },
        },
        {
          provide: AuthProvidersService,
          useValue: {
            load: vi.fn().mockResolvedValue(undefined),
            googleEnabled: googleEnabled.asReadonly(),
            googleClientId: signal('client').asReadonly(),
          },
        },
        {
          provide: GoogleGisLoader,
          useValue: {
            load: vi.fn().mockResolvedValue({
              initialize: vi.fn(),
              renderButton: vi.fn(),
              disableAutoSelect: vi.fn(),
            }),
            disableAutoSelect: vi.fn(),
          },
        },
        {
          provide: AuthApi,
          useValue: {
            issueGoogleNonce: vi.fn().mockResolvedValue({
              nonce: 'n',
              expiresAt: new Date(Date.now() + 60_000).toISOString(),
            }),
          },
        },
        { provide: ToastService, useValue: { show: vi.fn() } },
      ],
    }).compileComponents();
  });

  it('renders password and linked Google cards with soft-red disconnect', async () => {
    const fixture = TestBed.createComponent(AccountPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="password-card"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="google-card"]')).toBeTruthy();
    expect(el.textContent).toContain('فصل الربط');
    expect(el.textContent).toContain('g@example.com');
    expect(el.querySelector('[data-testid="account-footer-note"]')).toBeTruthy();
  });

  it('hides the Google card when providers disable Google', async () => {
    googleEnabled.set(false);
    const fixture = TestBed.createComponent(AccountPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="google-card"]'),
    ).toBeNull();
  });

  it('disables disconnect when Google is the only sign-in method', async () => {
    getSignInMethods.mockResolvedValue({
      password: { enabled: false },
      google: { linked: true, email: 'g@example.com' },
    });
    const fixture = TestBed.createComponent(AccountPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="last-method-hint"]')).toBeTruthy();
    const disconnect = Array.from(el.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('فصل الربط'),
    );
    expect(disconnect?.disabled || disconnect?.getAttribute('aria-disabled')).toBeTruthy();
  });

  it('shows the GIS link control when Google is not linked', async () => {
    getSignInMethods.mockResolvedValue({
      password: { enabled: true },
      google: { linked: false, email: null },
    });
    const fixture = TestBed.createComponent(AccountPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="google-link"]'),
    ).toBeTruthy();
  });
});
