import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { CompanySelectionService } from '../../core/auth/company-selection.service';
import { GoogleSignInFlow, googleAlertKindForCode } from './google-sign-in.flow';

function fakeCredential(payload: Record<string, unknown>): string {
  const body = btoa(JSON.stringify(payload)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `hdr.${body}.sig`;
}

describe('GoogleSignInFlow', () => {
  let loginWithGoogle: ReturnType<typeof vi.fn>;
  let selection: CompanySelectionService;
  let router: Router;

  beforeEach(() => {
    loginWithGoogle = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'login/choose-company', children: [] },
          { path: '', children: [] },
        ]),
        GoogleSignInFlow,
        CompanySelectionService,
        { provide: AuthService, useValue: { loginWithGoogle } },
      ],
    });
    selection = TestBed.inject(CompanySelectionService);
    router = TestBed.inject(Router);
  });

  it('stores selection in memory and navigates to choose-company', async () => {
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    loginWithGoogle.mockResolvedValue({
      kind: 'tenant_selection',
      selectionToken: 'sel',
      companies: [{ tenantId: 't1', name: 'Co', roleName: 'Owner', lastLoginAt: null }],
    });

    const credential = fakeCredential({ email: 'user@example.com', name: 'User' });
    const result = await TestBed.inject(GoogleSignInFlow).signIn(credential);

    expect(result).toEqual({ kind: 'choose_company' });
    expect(selection.state()).toEqual({
      selectionToken: 'sel',
      companies: [{ tenantId: 't1', name: 'Co', roleName: 'Owner', lastLoginAt: null }],
      email: 'user@example.com',
    });
    expect(navigate).toHaveBeenCalledWith('/login/choose-company');
  });

  it('maps Google error codes to alert kinds', () => {
    expect(googleAlertKindForCode('auth.google_account_not_found')).toBe('not_found');
    expect(googleAlertKindForCode('auth.google_email_unverified')).toBe('unverified');
    expect(googleAlertKindForCode('auth.email_not_verified')).toBe('kadr_unverified');
    expect(googleAlertKindForCode('auth.account_disabled')).toBe('deactivated');
    expect(googleAlertKindForCode('other')).toBe('generic');
  });
});
