import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthApi } from '../../../core/api/auth.api';
import { AuthProvidersService } from '../../../core/auth/auth-providers.service';
import { GoogleAccountsId, GoogleGisLoader } from '../../../core/auth/google-gis.loader';
import { ThemeService } from '../../../core/theme/theme.service';
import { ButtonComponent } from '../button/button.component';
import { SkeletonComponent } from '../skeleton/skeleton.component';

/** Library limits for the official button width (CSS px). */
export const GIS_MIN_WIDTH = 200;
export const GIS_MAX_WIDTH = 400;
/** Refresh the single-use nonce this long before it expires. */
const NONCE_REFRESH_MARGIN_MS = 30_000;
const NONCE_REFRESH_MIN_MS = 5_000;

export type GoogleButtonText = 'signin_with' | 'signup_with' | 'continue_with';
export type GoogleButtonState = 'loading' | 'ready' | 'hidden' | 'error';

/**
 * Official Google Identity Services button (white, thin border, localized by GIS itself).
 * Initialised with the public client id and a fresh single-use nonce from the API.
 * Emits the ID token string; the API is the only place that validates it.
 */
@Component({
  selector: 'kadr-google-sign-in-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent, SkeletonComponent],
  host: { class: 'block w-full' },
  template: `
    @if (state() === 'loading') {
      <div role="status" [attr.aria-label]="'auth.google.loading' | transloco">
        <kadr-skeleton height="2.5rem" />
      </div>
    }
    @if (state() === 'error') {
      <div
        class="flex flex-col items-start gap-2 rounded-md border border-line bg-subtle px-3 py-3 text-start"
        role="alert"
        data-testid="google-button-error"
      >
        <p class="text-sm text-fg">{{ 'auth.google.loadFailed' | transloco }}</p>
        <kadr-button variant="secondary" size="sm" type="button" (click)="retry()">
          {{ 'auth.google.retry' | transloco }}
        </kadr-button>
      </div>
    }
    <div
      #buttonHost
      class="flex w-full justify-center"
      [class.hidden]="state() !== 'ready'"
      [class.opacity-50]="disabled()"
      [attr.inert]="disabled() ? '' : null"
      [attr.aria-disabled]="disabled() ? 'true' : null"
      data-testid="google-button-host"
    ></div>
  `,
})
export class GoogleSignInButtonComponent {
  private readonly providers = inject(AuthProvidersService);
  private readonly loader = inject(GoogleGisLoader);
  private readonly api = inject(AuthApi);
  private readonly theme = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly text = input<GoogleButtonText>('signin_with');
  readonly disabled = input(false);
  readonly credential = output<string>();

  protected readonly state = signal<GoogleButtonState>('loading');
  private readonly buttonHost = viewChild.required<ElementRef<HTMLElement>>('buttonHost');

  private gis: GoogleAccountsId | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;
  private destroyed = false;

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.clearTimer();
    });
    afterNextRender(() => void this.init());

    // Re-render the official button when the UI language or label variant changes.
    effect(() => {
      this.theme.language();
      this.text();
      untracked(() => {
        if (this.gis && this.state() === 'ready') {
          this.render();
        }
      });
    });
  }

  /** Re-initialise with a fresh nonce. Call after a failed attempt ("try again"). */
  async refresh(): Promise<void> {
    if (!this.gis) {
      return this.init();
    }
    try {
      await this.prepare();
    } catch {
      this.state.set('error');
    }
  }

  protected retry(): void {
    void this.init();
  }

  private async init(): Promise<void> {
    this.state.set('loading');
    try {
      await this.providers.load();
      if (!this.providers.googleEnabled()) {
        this.state.set('hidden');
        return;
      }
      this.gis = await this.loader.load();
      await this.prepare();
    } catch {
      if (!this.destroyed) {
        this.state.set('error');
      }
    }
  }

  private async prepare(): Promise<void> {
    const gis = this.gis;
    const clientId = this.providers.googleClientId();
    if (!gis || !clientId) {
      throw new Error('gis_not_ready');
    }
    const nonce = await this.api.issueGoogleNonce();
    if (this.destroyed) {
      return;
    }
    gis.initialize({
      client_id: clientId,
      nonce: nonce.nonce,
      callback: (response) => this.onCredential(response.credential),
      auto_select: false,
      ux_mode: 'popup',
      cancel_on_tap_outside: true,
    });
    this.state.set('ready');
    this.render();
    this.scheduleRefresh(nonce.expiresAt);
  }

  private render(): void {
    const gis = this.gis;
    if (!gis) {
      return;
    }
    const target = this.buttonHost().nativeElement;
    target.replaceChildren();
    const available = this.host.nativeElement.getBoundingClientRect().width;
    const width = Math.min(
      GIS_MAX_WIDTH,
      Math.max(GIS_MIN_WIDTH, Math.floor(available) || GIS_MAX_WIDTH),
    );
    gis.renderButton(target, {
      type: 'standard',
      theme: 'outline',
      size: 'large',
      text: this.text(),
      shape: 'rectangular',
      logo_alignment: 'left',
      width,
      locale: this.theme.language(),
    });
  }

  private onCredential(credential: string | undefined): void {
    if (credential) {
      this.credential.emit(credential);
    }
    // The nonce is single-use: get a new one for the next attempt.
    void this.refresh();
  }

  private scheduleRefresh(expiresAt: string): void {
    this.clearTimer();
    const parsed = Date.parse(expiresAt);
    if (Number.isNaN(parsed)) {
      return;
    }
    const delay = Math.max(NONCE_REFRESH_MIN_MS, parsed - Date.now() - NONCE_REFRESH_MARGIN_MS);
    this.refreshTimer = setTimeout(() => void this.refresh(), delay);
  }

  private clearTimer(): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }
  }
}
