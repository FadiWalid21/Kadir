import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';

export const GOOGLE_GIS_SCRIPT_URL = 'https://accounts.google.com/gsi/client';

export interface GisCredentialResponse {
  credential?: string;
}

export interface GisInitializeConfig {
  client_id: string;
  nonce: string;
  callback: (response: GisCredentialResponse) => void;
  auto_select?: boolean;
  ux_mode?: 'popup' | 'redirect';
  cancel_on_tap_outside?: boolean;
}

export interface GisButtonConfig {
  type?: 'standard' | 'icon';
  theme?: 'outline' | 'filled_blue' | 'filled_black';
  size?: 'large' | 'medium' | 'small';
  text?: 'signin_with' | 'signup_with' | 'continue_with' | 'signin';
  shape?: 'rectangular' | 'pill';
  logo_alignment?: 'left' | 'center';
  width?: number;
  locale?: string;
}

export interface GoogleAccountsId {
  initialize(config: GisInitializeConfig): void;
  renderButton(parent: HTMLElement, options: GisButtonConfig): void;
  disableAutoSelect(): void;
}

interface GoogleGlobal {
  accounts?: { id?: GoogleAccountsId };
}

/**
 * Loads the Google Identity Services script on demand. Only pages that show the Google
 * button call `load()`, so no Google request is made anywhere else.
 */
@Injectable({ providedIn: 'root' })
export class GoogleGisLoader {
  private readonly document = inject(DOCUMENT);
  private pending: Promise<GoogleAccountsId> | null = null;

  load(): Promise<GoogleAccountsId> {
    const existing = this.accountsId();
    if (existing) {
      return Promise.resolve(existing);
    }
    if (!this.pending) {
      this.pending = new Promise<GoogleAccountsId>((resolve, reject) => {
        const script = this.document.createElement('script');
        script.src = GOOGLE_GIS_SCRIPT_URL;
        script.async = true;
        script.defer = true;
        script.onload = () => {
          const api = this.accountsId();
          if (api) {
            resolve(api);
          } else {
            script.remove();
            reject(new Error('gis_unavailable'));
          }
        };
        script.onerror = () => {
          script.remove();
          reject(new Error('gis_load_failed'));
        };
        this.document.head.appendChild(script);
      }).catch((error: unknown) => {
        this.pending = null;
        throw error;
      });
    }
    return this.pending;
  }

  /** Best effort: only acts when the script was already loaded on this page session. */
  disableAutoSelect(): void {
    this.accountsId()?.disableAutoSelect();
  }

  private accountsId(): GoogleAccountsId | null {
    const view = this.document.defaultView as (Window & { google?: GoogleGlobal }) | null;
    return view?.google?.accounts?.id ?? null;
  }
}
