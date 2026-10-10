import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { LoginPage } from './login.page';

describe('LoginPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        {
          provide: AuthService,
          useValue: {
            login: vi.fn().mockResolvedValue(undefined),
            loginErrorKey: () => 'auth.login_failed',
            status: signal('anonymous'),
          },
        },
      ],
    }).compileComponents();
  });

  it('renders login form in auth shell', async () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('كادر');
    expect(el.textContent).toContain('تسجيل الدخول');
    expect(el.querySelector('form')).toBeTruthy();
    expect(el.querySelector('kadr-button')).toBeTruthy();
    expect(el.querySelector('img[src="/brand/logo-kadr.png"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="auth-shell-card"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="auth-brand-panel"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="auth-sso-row"]')).toBeTruthy();
    expect(el.textContent).toContain('رمز الشركة');
  });
});
