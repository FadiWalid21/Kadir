import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { AuthShellLayout } from './auth-shell.layout';

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
  ],
  template: `
    <kadr-auth-shell
      [title]="'auth.loginTitle' | transloco"
      [subtitle]="'auth.loginSubtitle' | transloco"
    >
      <div class="flex flex-col gap-4">
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2" data-testid="auth-sso-row">
          <button
            type="button"
            disabled
            class="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-line bg-subtle px-3 text-sm text-fg-muted"
            [attr.title]="'auth.ssoUnavailable' | transloco"
            [attr.aria-label]="
              ('auth.ssoNafath' | transloco) + ' — ' + ('auth.ssoUnavailable' | transloco)
            "
          >
            <kadr-icon name="fingerprint" size="sm" />
            <span class="truncate">{{ 'auth.ssoNafath' | transloco }}</span>
          </button>
          <button
            type="button"
            disabled
            class="inline-flex h-11 items-center justify-center gap-2 rounded-md border border-line bg-subtle px-3 text-sm text-fg-muted"
            [attr.title]="'auth.ssoUnavailable' | transloco"
            [attr.aria-label]="
              ('auth.ssoGoogle' | transloco) + ' — ' + ('auth.ssoUnavailable' | transloco)
            "
          >
            <kadr-icon name="globe" size="sm" />
            <span class="truncate">{{ 'auth.ssoGoogle' | transloco }}</span>
          </button>
        </div>

        <div class="flex items-center gap-3 text-xs text-fg-muted" aria-hidden="true">
          <span class="h-px flex-1 bg-line"></span>
          <span>{{ 'auth.orEmail' | transloco }}</span>
          <span class="h-px flex-1 bg-line"></span>
        </div>

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
            [value]="email()"
            [error]="fieldErrors().email"
            (valueChange)="email.set($event)"
          />
          <kadr-input
            [label]="'auth.password' | transloco"
            type="password"
            name="password"
            [value]="password()"
            [error]="fieldErrors().password"
            (valueChange)="password.set($event)"
          />

          @if (formError()) {
            <p class="text-sm text-danger" role="alert">{{ formError()! | transloco }}</p>
          }

          <kadr-button variant="primary" type="submit" [loading]="submitting()" class="w-full">
            {{ 'auth.signIn' | transloco }}
          </kadr-button>
        </form>
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
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);

  readonly companyCode = signal('');
  readonly email = signal('');
  readonly password = signal('');
  readonly submitting = signal(false);
  readonly formError = signal<string | null>(null);
  readonly fieldErrors = signal<{
    companyCode?: string;
    email?: string;
    password?: string;
  }>({});

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.formError.set(null);

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
      await this.router.navigateByUrl('/');
    } catch (error) {
      this.formError.set(this.auth.loginErrorKey(error));
    } finally {
      this.submitting.set(false);
    }
  }
}
