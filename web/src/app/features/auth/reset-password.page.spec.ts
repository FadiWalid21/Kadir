import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApi } from '../../core/api/auth.api';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { ResetPasswordPage } from './reset-password.page';

describe('ResetPasswordPage', () => {
  let navigate: ReturnType<typeof vi.fn>;
  let resetPassword: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    resetPassword = vi.fn().mockResolvedValue(undefined);
    navigate = vi.fn().mockResolvedValue(true);
    await TestBed.configureTestingModule({
      imports: [ResetPasswordPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        { provide: AuthApi, useValue: { resetPassword } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: { get: (k: string) => (k === 'token' ? 'secret-token' : null) } },
          },
        },
        { provide: Router, useValue: { navigate } },
      ],
    }).compileComponents();
  });

  it('strips the token from the URL immediately and keeps it only in memory', async () => {
    const fixture = TestBed.createComponent(ResetPasswordPage);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({
        queryParams: { token: null },
        replaceUrl: true,
      }),
    );
    expect(sessionStorage.length).toBe(0);
    expect(localStorage.getItem('token')).toBeNull();

    const page = fixture.componentInstance;
    page.password.set('Str0ngPass!!');
    page.confirm.set('Str0ngPass!!');
    await page.onSubmit(new Event('submit'));
    expect(resetPassword).toHaveBeenCalledWith({
      token: 'secret-token',
      newPassword: 'Str0ngPass!!',
    });
  });

  it('shows invalid state when the query token is missing', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [ResetPasswordPage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        { provide: AuthApi, useValue: { resetPassword } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: { get: () => null } } },
        },
        { provide: Router, useValue: { navigate } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ResetPasswordPage);
    fixture.detectChanges();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="reset-invalid"]'),
    ).toBeTruthy();
  });
});
