import { inject, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { AuthService, GoogleErrorCodes } from '../../core/auth/auth.service';
import { CompanySelectionService } from '../../core/auth/company-selection.service';
import { readGoogleCredentialClaims } from '../../core/auth/google-credential';

export type GoogleFlowResult =
  | { kind: 'signed_in' }
  | { kind: 'choose_company' }
  | {
      kind: 'not_found';
      registrationToken: string;
      /** Display-only values read from the credential; the API holds the verified identity. */
      email: string | null;
      name: string | null;
    }
  | { kind: 'error'; code: string | null };

export type GoogleAlertKind = 'not_found' | 'unverified' | 'kadr_unverified' | 'deactivated' | 'generic';

export function googleAlertKindForCode(code: string | null | undefined): GoogleAlertKind {
  switch (code) {
    case GoogleErrorCodes.accountNotFound:
      return 'not_found';
    case GoogleErrorCodes.emailUnverified:
      return 'unverified';
    case GoogleErrorCodes.emailNotVerified:
      return 'kadr_unverified';
    case GoogleErrorCodes.accountDisabled:
      return 'deactivated';
    default:
      return 'generic';
  }
}

/** Shared by the login page and the wizard: one Google credential in, one next step out. */
@Injectable({ providedIn: 'root' })
export class GoogleSignInFlow {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly selection = inject(CompanySelectionService);

  async signIn(credential: string): Promise<GoogleFlowResult> {
    const claims = readGoogleCredentialClaims(credential);
    try {
      const outcome = await this.auth.loginWithGoogle(credential);
      switch (outcome.kind) {
        case 'signed_in':
          await this.router.navigateByUrl('/');
          return { kind: 'signed_in' };
        case 'tenant_selection':
          this.selection.set({
            selectionToken: outcome.selectionToken,
            companies: outcome.companies,
            email: claims?.email ?? null,
          });
          await this.router.navigateByUrl('/login/choose-company');
          return { kind: 'choose_company' };
        case 'not_found':
          return {
            kind: 'not_found',
            registrationToken: outcome.registrationToken,
            email: claims?.email ?? null,
            name: claims?.name ?? null,
          };
      }
    } catch (error) {
      return { kind: 'error', code: problemDetailsFromError(error)?.code ?? null };
    }
  }
}
