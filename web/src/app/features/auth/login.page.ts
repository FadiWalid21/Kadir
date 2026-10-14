import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthProvidersService } from '../../core/auth/auth-providers.service';
import { AuthService } from '../../core/auth/auth.service';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { GoogleSignInButtonComponent } from '../../shared/ui/google-sign-in-button/google-sign-in-button.component';
import { IconComponent, IconName } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { OnboardingStore } from '../onboarding/onboarding.store';
import { AuthAlertComponent, AuthAlertTone } from './auth-alert.component';
import { AuthShellLayout } from './auth-shell.layout';
import { GoogleAlertKind, GoogleSignInFlow, googleAlertKindForCode } from './google-sign-in.flow';

interface GoogleAlert {
  kind: GoogleAlertKind;
  registration?: { token: string; email: string | null; name: string | null };
}

const ALERT_PRESENTATION: Record<GoogleAlertKind, { tone: AuthAlertTone; icon: IconName }> = {
  not_found: { tone: 'info', icon: 'info' },
  unverified: { tone: 'warning', icon: 'alert' },
  kadr_unverified: { tone: 'warning', icon: 'alert' },
  deactivated: { tone: 'danger', icon: 'lock' },
  generic: { tone: 'warning', icon: 'alert' },
};

@Component({
  selector: 'kadr-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    RouterLink,
    ButtonComponent,
    InputComponent,
    IconComponent,
    AuthShellLayout,
    AuthAlertComponent,
    GoogleSignInButtonComponent,
  ],
  template: `
    <kadr-auth-shell
      [title]="'auth.loginTitle' | transloco"
      [subtitle]="'auth.loginSubtitle' | transloco"
    >
      <div class="flex flex-col gap-4">
        @if (alertView(); as view) {
          <kadr-auth-alert
            [tone]="view.tone"
            [icon]="view.icon"
            [title]="('auth.googleAlert.' + view.kind + '.title') | transloco"
            [message]="('auth.googleAlert.' + view.kind + '.body') | transloco"
            data-testid="google-alert"
            [attr.data-kind]="view.kind"
          >
            @switch (view.kind) {
              @case ('not_found') {
                <button
                  type="button"
                  class="inline-flex items-center gap-1 font-medium text-accent underline-offset-2 hover:underline focus-visible:outline-none"
                  data-testid="google-alert-create"
                  (click)="createCompanyFromGoogle()"
                >
                  {{ 'auth.googleAlert.not_found.create' | transloco }}
                  <kadr-icon name="chevron-end" size="sm" [rtlFlip]="true" />
                </button>
                <button
                  type="button"
                  class="text-fg-2 underline-offset-2 hover:underline focus-visible:outline-none"
                  data-testid="google-alert-password"
                  (click)="usePassword()"
                >
                  {{ 'auth.googleAlert.not_found.usePassword' | transloco }}
                </button>
              }
              @case ('kadr_unverified') {
                <a
                  routerLink="/verify-email"
                  class="font-medium text-accent underline-offset-2 hover:underline"
                >
                  {{ 'auth.verifyEmailLink' | transloco }}
                </a>
              }
              @case ('generic') {
                <button
                  type="button"
                  class="font-medium text-accent underline-offset-2 hover:underline focus-visible:outline-none"
                  data-testid="google-alert-retry"
                  (click)="retryGoogle()"
                >
                  {{ 'auth.googleAlert.generic.retry' | transloco }}
                </button>
              }
            }
          </kadr-auth-alert>
        }

        <form class="flex flex-col gap-4" (submit)="onSubmit($event)">
          <kadr-input
            [label]="'auth.companyCode' | transloco"
            name="companyCode"
            [value]="companyCode()"
            [error]="fieldErrors().companyCode"
            (valueChange)="companyCode.set($event)"
          />
          <kadr-input
            [label]="'ui.email' | transloco"
            type="email"
            name="email"
            autocomplete="username"
            [value]="email()"
            [error]="fieldErrors().email"
            (valueChange)="email.set($event)"
          />
          <div class="flex flex-col gap-2">
            <kadr-input
              [label]="'auth.password' | transloco"
              type="password"
              name="password"
              autocomplete="current-password"
              [revealable]="true"
              [showLabel]="'auth.showPassword' | transloco"
              [hideLabel]="'auth.hidePassword' | transloco"
              [value]="password()"
              [error]="fieldErrors().password"
              (valueChange)="password.set($event)"
            />
            <a
              routerLink="/forgot-password"
              class="w-fit self-end text-sm text-accent underline-offset-2 hover:underline"
              data-testid="forgot-password-link"
            >
              {{ 'auth.forgotPasswordLink' | transloco }}
            </a>
          </div>

          @if (formError()) {
            <p class="text-sm text-danger" role="alert">{{ formError()! | transloco }}</p>
          }

          <kadr-button
            variant="primary"
            type="submit"
            [loading]="submitting()"
            [disabled]="googleBusy()"
            class="w-full"
          >
            {{ 'auth.signIn' | transloco }}
          </kadr-button>
        </form>

        @if (googleEnabled()) {
          <div class="flex flex-col gap-3" data-testid="google-section">
            <div class="flex items-center gap-3 text-sm text-fg-muted" aria-hidden="true">
              <span class="h-px flex-1 bg-line"></span>
              <span>{{ 'auth.or' | transloco }}</span>
              <span class="h-px flex-1 bg-line"></span>
            </div>
            <kadr-google-sign-in-button
              text="signin_with"
              [disabled]="submitting() || googleBusy()"
              (credential)="onGoogleCredential($event)"
            />
            @if (googleBusy()) {
              <p class="text-center text-sm text-fg-2" role="status" data-testid="google-busy">
                {{ 'auth.google.verifying' | transloco }}
              </p>
            }
            <p class="flex items-center justify-center gap-2 text-center text-xs text-fg-muted">
              <kadr-icon name="shield" size="sm" />
              {{ 'auth.google.note' | transloco }}
            </p>
          </div>
        }
      </div>

      <div
        authFooter
        class="flex flex-col gap-3 rounded-md border border-line bg-subtle px-4 py-3 text-start"
      >
        <p class="text-sm text-fg-2">{{ 'auth.registerPrompt' | transloco }}</p>
        <a
          routerLink="/register"
          class="inline-flex items-center gap-2 text-sm font-medium text-accent underline-offset-2 hover:underline"
        >
          <kadr-icon name="building" size="sm" />
          {{ 'auth.registerCta' | transloco }}
        </a>
        <a
          routerLink="/verify-email"
          class="text-xs text-fg-muted underline-offset-2 hover:underline"
        >
          {{ 'auth.verifyEmailLink' | transloco }}
        </a>
      </div>
    </kadr-auth-shell>
  `,
})
export class LoginPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly providers = inject(AuthProvidersService);
  private readonly googleFlow = inject(GoogleSignInFlow);
  private readonly onboarding = inject(OnboardingStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly googleButton = viewChild(GoogleSignInButtonComponent);

  readonly companyCode = signal('');
  readonly email = signal('');
  readonly password = signal('');
  readonly submitting = signal(false);
  readonly googleBusy = signal(false);
  readonly formError = signal<string | null>(null);
  readonly alert = signal<GoogleAlert | null>(null);
  readonly fieldErrors = signal<{
    companyCode?: string;
    email?: string;
    password?: string;
  }>({});

  /** Google section is shown only when the API says Google is enabled. */
  readonly googleEnabled = computed(() => this.providers.googleEnabled());

  protected readonly alertView = computed(() => {
    const alert = this.alert();
    return alert ? { kind: alert.kind, ...ALERT_PRESENTATION[alert.kind] } : null;
  });

  ngOnInit(): void {
    void this.providers.load();
  }

  async onGoogleCredential(credential: string): Promise<void> {
    if (this.googleBusy() || this.submitting()) {
      return;
    }
    this.alert.set(null);
    this.formError.set(null);
    this.googleBusy.set(true);
    try {
      const result = await this.googleFlow.signIn(credential);
      switch (result.kind) {
        case 'not_found':
          this.alert.set({
            kind: 'not_found',
            registration: {
              token: result.registrationToken,
              email: result.email,
              name: result.name,
            },
          });
          break;
        case 'error':
          this.alert.set({ kind: googleAlertKindForCode(result.code) });
          break;
        default:
          break;
      }
    } finally {
      this.googleBusy.set(false);
    }
  }

  async createCompanyFromGoogle(): Promise<void> {
    const registration = this.alert()?.registration;
    if (!registration) {
      return;
    }
    this.onboarding.startGoogleRegistration({
      token: registration.token,
      email: registration.email,
      fullName: registration.name,
    });
    this.alert.set(null);
    await this.router.navigateByUrl('/register');
  }

  usePassword(): void {
    this.alert.set(null);
    this.host.nativeElement.querySelector<HTMLInputElement>('input[name="companyCode"]')?.focus();
  }

  retryGoogle(): void {
    this.alert.set(null);
    void this.googleButton()?.refresh();
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.formError.set(null);
    this.alert.set(null);

    const slug = this.companyCode().trim();
    const email = this.email().trim();
    const password = this.password();
    const errors: { companyCode?: string; email?: string; password?: string } = {};

    if (!slug) {
      errors.companyCode = this.transloco.translate('auth.companyCodeRequired');
    }
    if (!email || !email.includes('@')) {
      errors.email = this.transloco.translate('ui.emailError');
    }
    if (!password) {
      errors.password = this.transloco.translate('auth.passwordRequired');
    }

    this.fieldErrors.set(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    this.submitting.set(true);
    try {
      await this.auth.login(slug, email, password);
      await this.router.navigateByUrl('/home');
    } catch (error) {
      this.formError.set(this.auth.loginErrorKey(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
