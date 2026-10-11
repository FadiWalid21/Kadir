import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApi } from '../../core/api/auth.api';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { ForgotPasswordPage } from './forgot-password.page';

describe('ForgotPasswordPage', () => {
  let forgotPassword: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    forgotPassword = vi.fn().mockResolvedValue({ status: 'accepted' });
    await TestBed.configureTestingModule({
      imports: [ForgotPasswordPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        { provide: AuthApi, useValue: { forgotPassword } },
      ],
    }).compileComponents();
  });

  it('shows a neutral sent state after submit (anti-enumeration)', async () => {
    const fixture = TestBed.createComponent(ForgotPasswordPage);
    const page = fixture.componentInstance;
    fixture.detectChanges();

    page.email.set('anyone@example.com');
    await page.onSubmit(new Event('submit'));
    fixture.detectChanges();

    expect(forgotPassword).toHaveBeenCalledWith({ email: 'anyone@example.com' });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="forgot-sent"]')).toBeTruthy();
    expect(el.textContent).toContain('إذا كان هناك حساب مرتبط بهذا البريد');
  });
});
