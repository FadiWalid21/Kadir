import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { InputComponent } from '../../shared/ui/input/input.component';

@Component({
  selector: 'kadr-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, ButtonComponent, InputComponent],
  template: `
    <main class="flex min-h-dvh flex-col items-center justify-center bg-app px-4 py-10">
      <div
        class="w-full max-w-md rounded-lg border border-line bg-surface p-6 shadow-sm sm:p-8"
      >
        <header class="mb-8 flex flex-col items-start gap-4 text-start">
          <img
            src="/brand/logo-kadr.png"
            alt=""
            width="56"
            height="56"
            class="size-14 object-contain"
          />
          <div class="flex flex-col gap-2">
            <p class="text-sm font-medium text-accent">{{ 'app.name' | transloco }}</p>
            <h1 class="text-2xl font-bold leading-tight text-fg">
              {{ 'auth.loginTitle' | transloco }}
            </h1>
            <p class="text-sm text-fg-2">{{ 'auth.loginSubtitle' | transloco }}</p>
          </div>
        </header>

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

          <kadr-button variant="primary" type="submit" [loading]="submitting()" class="mt-2 w-full">
            {{ 'auth.signIn' | transloco }}
          </kadr-button>
        </form>

        <p class="mt-6 text-center text-sm text-fg-muted">
          <a routerLink="/verify-email" class="text-accent underline-offset-2 hover:underline">
            {{ 'auth.verifyEmailLink' | transloco }}
          </a>
        </p>
      </div>
    </main>
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
