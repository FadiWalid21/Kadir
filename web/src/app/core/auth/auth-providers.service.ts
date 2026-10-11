import { inject, Injectable, signal } from '@angular/core';
import { AuthApi } from '../api/auth.api';

export type ProvidersStatus = 'idle' | 'loading' | 'ready' | 'error';

/** Loads `GET /auth/providers` once per session; Google UI is shown only when it reports enabled. */
@Injectable({ providedIn: 'root' })
export class AuthProvidersService {
  private readonly api = inject(AuthApi);

  private readonly statusSignal = signal<ProvidersStatus>('idle');
  private readonly googleEnabledSignal = signal(false);
  private readonly googleClientIdSignal = signal<string | null>(null);
  private inFlight: Promise<void> | null = null;

  readonly status = this.statusSignal.asReadonly();
  readonly googleEnabled = this.googleEnabledSignal.asReadonly();
  readonly googleClientId = this.googleClientIdSignal.asReadonly();

  /** Resolves when providers are known. A failed request leaves Google hidden and can be retried. */
  load(): Promise<void> {
    if (this.statusSignal() === 'ready') {
      return Promise.resolve();
    }
    if (!this.inFlight) {
      this.statusSignal.set('loading');
      this.inFlight = this.api
        .getProviders()
        .then((response) => {
          const google = response.google;
          const clientId = google?.clientId?.trim() || null;
          this.googleEnabledSignal.set(!!google?.enabled && !!clientId);
          this.googleClientIdSignal.set(clientId);
          this.statusSignal.set('ready');
        })
        .catch(() => {
          this.googleEnabledSignal.set(false);
          this.googleClientIdSignal.set(null);
          this.statusSignal.set('error');
        })
        .finally(() => {
          this.inFlight = null;
        });
    }
    return this.inFlight;
  }
}
