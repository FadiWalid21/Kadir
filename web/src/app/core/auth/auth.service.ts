import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthApi, problemDetailsFromError } from '../api/auth.api';
import { GoogleCompanyOption, isTenantSelection } from '../api/auth.models';
import { MeApi } from '../api/me.api';
import { MeResponse } from '../api/me.models';
import { SubscriptionStore } from '../subscription/subscription.store';
import { PermissionCode } from './permission-codes';

export type AuthStatus = 'unknown' | 'authenticated' | 'anonymous';

export const GoogleErrorCodes = {
  accountNotFound: 'auth.google_account_not_found',
  emailUnverified: 'auth.google_email_unverified',
  emailNotVerified: 'auth.email_not_verified',
  accountDisabled: 'auth.account_disabled',
  selectionInvalid: 'auth.google_selection_invalid',
} as const;

export type GoogleLoginOutcome =
  | { kind: 'signed_in' }
  | { kind: 'tenant_selection'; selectionToken: string; companies: GoogleCompanyOption[] }
  | { kind: 'not_found'; registrationToken: string };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authApi = inject(AuthApi);
  private readonly meApi = inject(MeApi);
  private readonly subscriptionStore = inject(SubscriptionStore);
  private readonly router = inject(Router);

  private readonly accessTokenSignal = signal<string | null>(null);
  private readonly meSignal = signal<MeResponse | null>(null);
  private readonly statusSignal = signal<AuthStatus>('unknown');

  private refreshInFlight: Promise<boolean> | null = null;
  private restoreInFlight: Promise<void> | null = null;

  readonly accessToken = this.accessTokenSignal.asReadonly();
  readonly me = this.meSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();

  hasPermission(code: PermissionCode | string): boolean {
    const me = this.meSignal();
    return !!me?.permissions.includes(code);
  }

  async restoreSession(): Promise<void> {
    if (this.statusSignal() !== 'unknown') {
      return;
    }
    if (this.restoreInFlight) {
      return this.restoreInFlight;
    }
    this.restoreInFlight = this.doRestore().finally(() => {
      this.restoreInFlight = null;
    });
    return this.restoreInFlight;
  }

  async login(companySlug: string, email: string, password: string): Promise<void> {
    const tokens = await this.authApi.login({ companySlug, email, password });
    this.accessTokenSignal.set(tokens.accessToken);
    const me = await this.meApi.getMe();
    this.setMe(me);
  }

  /**
   * Google sign-in with a GIS credential. Resolves with the next step; failures other than
   * "no account" throw the HTTP error so the caller can map `problemDetailsFromError(...).code`.
   */
  async loginWithGoogle(credential: string): Promise<GoogleLoginOutcome> {
    try {
      const response = await this.authApi.googleLogin(credential);
      if (isTenantSelection(response)) {
        return {
          kind: 'tenant_selection',
          selectionToken: response.selectionToken,
          companies: response.companies,
        };
      }
      await this.completeSignIn(response.accessToken);
      return { kind: 'signed_in' };
    } catch (error) {
      const problem = problemDetailsFromError(error);
      if (problem?.code === GoogleErrorCodes.accountNotFound && problem.registrationToken) {
        return { kind: 'not_found', registrationToken: problem.registrationToken };
      }
      throw error;
    }
  }

  async selectGoogleTenant(selectionToken: string, tenantId: string): Promise<void> {
    const tokens = await this.authApi.googleSelectTenant({ selectionToken, tenantId });
    await this.completeSignIn(tokens.accessToken);
  }

  async logout(): Promise<void> {
    try {
      await this.authApi.logout();
    } catch {
      // Clear local session even if the API call fails.
    }
    this.clearSession();
    await this.router.navigateByUrl('/login');
  }

  clearSession(): void {
    this.accessTokenSignal.set(null);
    this.meSignal.set(null);
    this.statusSignal.set('anonymous');
    this.subscriptionStore.clear();
  }

  /** Reload `/me` after tenant branding changes. */
  async refreshMe(): Promise<void> {
    const me = await this.meApi.getMe();
    this.setMe(me);
  }

  /**
   * Single-flight refresh shared by the interceptor and parallel 401s.
   * Returns true when a new access token was stored.
   */
  refreshAccessToken(): Promise<boolean> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.doRefresh().finally(() => {
        this.refreshInFlight = null;
      });
    }
    return this.refreshInFlight;
  }

  loginErrorKey(error: unknown): string {
    const problem = problemDetailsFromError(error);
    switch (problem?.code) {
      case 'auth.invalid_credentials':
        return 'auth.invalid_credentials';
      case 'auth.email_not_verified':
        return 'auth.email_not_verified';
      case 'auth.account_locked':
        return 'auth.account_locked';
      default:
        return 'auth.login_failed';
    }
  }

  private async completeSignIn(accessToken: string): Promise<void> {
    this.accessTokenSignal.set(accessToken);
    const me = await this.meApi.getMe();
    this.setMe(me);
  }

  private async doRestore(): Promise<void> {
    try {
      const ok = await this.refreshAccessToken();
      if (!ok) {
        this.clearSession();
        return;
      }
      const me = await this.meApi.getMe();
      this.setMe(me);
    } catch {
      this.clearSession();
    }
  }

  private setMe(me: MeResponse): void {
    this.meSignal.set(me);
    this.statusSignal.set('authenticated');
    this.subscriptionStore.applyFromMe(me.subscription);
  }

  private async doRefresh(): Promise<boolean> {
    try {
      const tokens = await this.authApi.refresh();
      this.accessTokenSignal.set(tokens.accessToken);
      return true;
    } catch {
      return false;
    }
  }
}
