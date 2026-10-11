import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { PlatformApi } from '../api/platform.api';

export type PlatformAuthStatus = 'unknown' | 'authenticated' | 'anonymous';

const EMAIL_STORAGE_KEY = 'kadr.platform.operatorEmail';

@Injectable({ providedIn: 'root' })
export class PlatformAuthService {
  private readonly api = inject(PlatformApi);
  private readonly router = inject(Router);

  private readonly accessTokenSignal = signal<string | null>(null);
  private readonly statusSignal = signal<PlatformAuthStatus>('unknown');
  private readonly adminIdSignal = signal<string | null>(null);
  private readonly emailSignal = signal<string | null>(this.readStoredEmail());

  private refreshInFlight: Promise<boolean> | null = null;
  private restoreInFlight: Promise<void> | null = null;

  readonly accessToken = this.accessTokenSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();
  readonly adminId = this.adminIdSignal.asReadonly();
  readonly email = this.emailSignal.asReadonly();

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

  async login(email: string, password: string): Promise<void> {
    const tokens = await this.api.login(email, password);
    this.accessTokenSignal.set(tokens.accessToken);
    const me = await this.api.me();
    this.adminIdSignal.set(me.id);
    this.setEmail(email.trim());
    this.statusSignal.set('authenticated');
  }

  async logout(): Promise<void> {
    try {
      await this.api.logout();
    } catch {
      /* clear local anyway */
    }
    this.clearSession();
    await this.router.navigateByUrl('/platform/login');
  }

  clearSession(): void {
    this.accessTokenSignal.set(null);
    this.adminIdSignal.set(null);
    this.emailSignal.set(null);
    try {
      sessionStorage.removeItem(EMAIL_STORAGE_KEY);
    } catch {
      /* private mode */
    }
    this.statusSignal.set('anonymous');
  }

  refreshAccessToken(): Promise<boolean> {
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.doRefresh().finally(() => {
        this.refreshInFlight = null;
      });
    }
    return this.refreshInFlight;
  }

  private setEmail(email: string | null): void {
    this.emailSignal.set(email);
    try {
      if (email) {
        sessionStorage.setItem(EMAIL_STORAGE_KEY, email);
      } else {
        sessionStorage.removeItem(EMAIL_STORAGE_KEY);
      }
    } catch {
      /* private mode */
    }
  }

  private readStoredEmail(): string | null {
    try {
      return sessionStorage.getItem(EMAIL_STORAGE_KEY);
    } catch {
      return null;
    }
  }

  private async doRestore(): Promise<void> {
    try {
      const ok = await this.refreshAccessToken();
      if (!ok) {
        this.clearSession();
        return;
      }
      const me = await this.api.me();
      this.adminIdSignal.set(me.id);
      this.emailSignal.set(this.readStoredEmail());
      this.statusSignal.set('authenticated');
    } catch {
      this.clearSession();
    }
  }

  private async doRefresh(): Promise<boolean> {
    try {
      const tokens = await this.api.refresh();
      this.accessTokenSignal.set(tokens.accessToken);
      return true;
    } catch {
      return false;
    }
  }
}
