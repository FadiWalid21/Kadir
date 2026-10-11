import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthApi, problemDetailsFromError } from '../../core/api/auth.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { MIN_PASSWORD_LENGTH } from '../onboarding/onboarding.store';
import { AuthShellLayout } from './auth-shell.layout';

type ResetState = 'form' | 'invalid' | 'success';

const RESET_INVALID = 'auth.password_reset_invalid';
const PASSWORD_TOO_SHORT = 'auth.password_too_short';
const PASSWORD_TOO_COMMON = 'auth.password_too_common';

@Component({
  selector: 'kadr-reset-password-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, ButtonComponent, InputComponent, IconComponent, AuthShellLayout],
  template: `
    <kadr-auth-shell
      [title]="'auth.reset.title' | transloco"
      [subtitle]="(state() === 'form' ? 'auth.reset.subtitle' : '') | transloco"
    >
      @switch (state()) {
        @case ('form') {
          <form class="flex flex-col gap-4" (submit)="onSubmit($event)" data-testid="reset-form">
            <kadr-input
              [label]="'auth.reset.newPassword' | transloco"
              type="password"
              name="newPassword"
              autocomplete="new-password"
              [revealable]="true"
              [showLabel]="'auth.showPassword' | transloco"
              [hideLabel]="'auth.hidePassword' | transloco"
              [hint]="'auth.passwordRules' | transloco"
              [value]="password()"
              [error]="passwordError()"
              (valueChange)="password.set($event)"
            />
            <kadr-input
              [label]="'auth.reset.confirmPassword' | transloco"
              type="password"
              name="confirmPassword"
              autocomplete="new-password"
              [revealable]="true"
              [showLabel]="'auth.showPassword' | transloco"
              [hideLabel]="'auth.hidePassword' | transloco"
              [value]="confirm()"
              [error]="confirmError()"
              (valueChange)="confirm.set($event)"
            />
            @if (formError()) {
              <p class="text-sm text-danger" role="alert">{{ formError()! | transloco }}</p>
            }
            <kadr-button variant="primary" type="submit" [loading]="submitting()" class="w-full">
              {{ 'auth.reset.submit' | transloco }}
            </kadr-button>
          </form>
        }
        @case ('invalid') {
          <div
            class="flex flex-col gap-4 rounded-md bg-warning-soft px-4 py-4 text-start"
            role="alert"
            data-testid="reset-invalid"
          >
            <div class="flex items-start gap-3">
              <span class="mt-0.5 text-warning"><kadr-icon name="alert" size="md" /></span>
              <div class="flex flex-col gap-1">
                <p class="text-sm font-bold text-fg">{{ 'auth.reset.invalidTitle' | transloco }}</p>
                <p class="text-sm text-fg">{{ 'auth.reset.invalidBody' | transloco }}</p>
              </div>
            </div>
            <a routerLink="/forgot-password" class="w-fit">
              <kadr-button variant="primary" type="button">
                {{ 'auth.reset.requestNew' | transloco }}
              </kadr-button>
            </a>
          </div>
        }
        @case ('success') {
          <div
            class="flex flex-col gap-4 rounded-md bg-success-soft px-4 py-4 text-start"
            role="status"
            data-testid="reset-success"
          >
            <div class="flex items-start gap-3">
              <span class="mt-0.5 text-success"><kadr-icon name="check" size="md" /></span>
              <div class="flex flex-col gap-1">
                <p class="text-sm font-bold text-fg">{{ 'auth.reset.successTitle' | transloco }}</p>
                <p class="text-sm text-fg">{{ 'auth.reset.successBody' | transloco }}</p>
              </div>
            </div>
            <a routerLink="/login" class="w-fit">
              <kadr-button variant="primary" type="button">
                {{ 'auth.reset.goToLogin' | transloco }}
              </kadr-button>
            </a>
          </div>
        }
      }

      <div authFooter class="text-start text-sm">
        <a routerLink="/login" class="text-accent underline-offset-2 hover:underline">
          {{ 'auth.backToLogin' | transloco }}
        </a>
      </div>
    </kadr-auth-shell>
  `,
})
export class ResetPasswordPage implements OnInit {
  private readonly api = inject(AuthApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  readonly state = signal<ResetState>('form');
  readonly password = signal('');
  readonly confirm = signal('');
  readonly submitting = signal(false);
  readonly passwordError = signal<string | undefined>(undefined);
  readonly confirmError = signal<string | undefined>(undefined);
  readonly formError = signal<string | null>(null);

  /** Held in memory only; removed from the URL as soon as it is read. */
  private token: string | null = null;

  ngOnInit(): void {
    this.suppressReferrer();

    const fromQuery = this.route.snapshot.queryParamMap.get('token');
    this.token = fromQuery?.trim() || null;
    if (fromQuery !== null) {
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { token: null },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
    }
    if (!this.token) {
      this.state.set('invalid');
    }
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.formError.set(null);
    this.passwordError.set(undefined);
    this.confirmError.set(undefined);

    if (this.password().length < MIN_PASSWORD_LENGTH) {
      this.passwordError.set(this.transloco.translate(PASSWORD_TOO_SHORT));
      return;
    }
    if (this.password() !== this.confirm()) {
      this.confirmError.set(this.transloco.translate('auth.passwordMismatch'));
      return;
    }
    if (!this.token) {
      this.state.set('invalid');
      return;
    }

    this.submitting.set(true);
    try {
      await this.api.resetPassword({ token: this.token, newPassword: this.password() });
      this.token = null;
      this.password.set('');
      this.confirm.set('');
      this.state.set('success');
    } catch (error) {
      const code = problemDetailsFromError(error)?.code;
      if (code === RESET_INVALID) {
        this.token = null;
        this.state.set('invalid');
      } else if (code === PASSWORD_TOO_SHORT || code === PASSWORD_TOO_COMMON) {
        this.passwordError.set(this.transloco.translate(code));
      } else {
        this.formError.set('auth.reset.failed');
      }
    } finally {
      this.submitting.set(false);
    }
  }

  /** The reset link carries a secret: never send it as a Referer while this page is open. */
  private suppressReferrer(): void {
    const meta = this.document.createElement('meta');
    meta.name = 'referrer';
    meta.content = 'no-referrer';
    this.document.head.appendChild(meta);
    this.destroyRef.onDestroy(() => meta.remove());
  }
}
