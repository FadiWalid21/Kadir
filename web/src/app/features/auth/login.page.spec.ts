import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApi } from '../../core/api/auth.api';
import { AuthProvidersService } from '../../core/auth/auth-providers.service';
import { AuthService } from '../../core/auth/auth.service';
import { GoogleGisLoader } from '../../core/auth/google-gis.loader';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { OnboardingStore } from '../onboarding/onboarding.store';
import { GoogleSignInFlow } from './google-sign-in.flow';
import { LoginPage } from './login.page';

describe('LoginPage', () => {
  const googleEnabled = signal(false);
  const loadProviders = vi.fn().mockResolvedValue(undefined);
  const signIn = vi.fn();
  const startGoogleRegistration = vi.fn();
  const gisApi = {
    initialize: vi.fn(),
    renderButton: vi.fn(),
    disableAutoSelect: vi.fn(),
  };

  beforeEach(async () => {
    googleEnabled.set(false);
    loadProviders.mockClear();
    signIn.mockReset();
    startGoogleRegistration.mockReset();
    Object.values(gisApi).forEach((fn) => fn.mockClear());

    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideRouter([{ path: 'register', children: [] }]),
        provideTestTransloco(),
        {
          provide: AuthService,
          useValue: {
            login: vi.fn().mockResolvedValue(undefined),
            loginErrorKey: () => 'auth.login_failed',
            status: signal('anonymous'),
          },
        },
        {
          provide: AuthProvidersService,
          useValue: {
            load: loadProviders,
            googleEnabled: googleEnabled.asReadonly(),
            googleClientId: signal('test-client').asReadonly(),
          },
        },
        {
          provide: GoogleGisLoader,
          useValue: {
            load: vi.fn().mockResolvedValue(gisApi),
            disableAutoSelect: vi.fn(),
          },
        },
        {
          provide: AuthApi,
          useValue: {
            issueGoogleNonce: vi.fn().mockResolvedValue({
              nonce: 'nonce-1',
              expiresAt: new Date(Date.now() + 300_000).toISOString(),
            }),
          },
        },
        {
          provide: GoogleSignInFlow,
          useValue: { signIn },
        },
        {
          provide: OnboardingStore,
          useValue: { startGoogleRegistration },
        },
      ],
    }).compileComponents();
  });

  it('renders the password form without the legacy SSO row', async () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('form')).toBeTruthy();
    expect(el.querySelector('[data-testid="auth-shell-card"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="forgot-password-link"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="auth-sso-row"]')).toBeNull();
    expect(el.querySelector('[data-testid="google-section"]')).toBeNull();
    expect(loadProviders).toHaveBeenCalled();
  });

  it('shows the Google divider and button host only when providers enable Google', async () => {
    googleEnabled.set(true);
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const section = el.querySelector('[data-testid="google-section"]');
    expect(section).toBeTruthy();
    expect(section?.textContent).toContain('أو');
    expect(section?.textContent).toContain('يعمل فقط مع الحسابات المسجلة أو المدعوة');
    expect(el.querySelector('kadr-google-sign-in-button')).toBeTruthy();
  });

  it('shows a not_found alert with create-company and use-password actions', async () => {
    googleEnabled.set(true);
    signIn.mockResolvedValue({
      kind: 'not_found',
      registrationToken: 'reg-token',
      email: 'new@example.com',
      name: 'New User',
    });

    const fixture = TestBed.createComponent(LoginPage);
    const page = fixture.componentInstance;
    fixture.detectChanges();
    await page.onGoogleCredential('fake.credential');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const alert = el.querySelector('[data-testid="google-alert"]');
    expect(alert?.getAttribute('data-kind')).toBe('not_found');
    expect(el.querySelector('[data-testid="google-alert-create"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="google-alert-password"]')).toBeTruthy();
  });

  it('routes create-company into the wizard with the registration token', async () => {
    googleEnabled.set(true);
    signIn.mockResolvedValue({
      kind: 'not_found',
      registrationToken: 'reg-token',
      email: 'new@example.com',
      name: 'New User',
    });
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    const fixture = TestBed.createComponent(LoginPage);
    const page = fixture.componentInstance;
    fixture.detectChanges();
    await page.onGoogleCredential('fake.credential');
    await page.createCompanyFromGoogle();

    expect(startGoogleRegistration).toHaveBeenCalledWith({
      token: 'reg-token',
      email: 'new@example.com',
      fullName: 'New User',
    });
    expect(navigate).toHaveBeenCalledWith('/register');
  });

  it('maps deactivated and generic Google errors to alerts', async () => {
    googleEnabled.set(true);
    const fixture = TestBed.createComponent(LoginPage);
    const page = fixture.componentInstance;
    fixture.detectChanges();

    signIn.mockResolvedValue({ kind: 'error', code: 'auth.account_disabled' });
    await page.onGoogleCredential('c1');
    fixture.detectChanges();
    expect(
      (fixture.nativeElement as HTMLElement)
        .querySelector('[data-testid="google-alert"]')
        ?.getAttribute('data-kind'),
    ).toBe('deactivated');

    signIn.mockResolvedValue({ kind: 'error', code: 'auth.google_token_invalid' });
    await page.onGoogleCredential('c2');
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="google-alert"]')?.getAttribute('data-kind')).toBe(
      'generic',
    );
    expect(el.querySelector('[data-testid="google-alert-retry"]')).toBeTruthy();
  });
});
