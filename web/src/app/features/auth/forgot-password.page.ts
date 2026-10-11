import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthApi } from '../../core/api/auth.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { AuthShellLayout } from './auth-shell.layout';

type ForgotState = 'form' | 'sent';

@Component({
  selector: 'kadr-forgot-password-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, ButtonComponent, InputComponent, IconComponent, AuthShellLayout],
  template: `
    <kadr-auth-shell
      [title]="'auth.forgot.title' | transloco"
      [subtitle]="(state() === 'form' ? 'auth.forgot.subtitle' : '') | transloco"
    >
      @if (state() === 'form') {
        <form class="flex flex-col gap-4" (submit)="onSubmit($event)" data-testid="forgot-form">
          <kadr-input
            [label]="'ui.email' | transloco"
            type="email"
            name="email"
            autocomplete="email"
            [value]="email()"
            [error]="emailError()"
            (valueChange)="email.set($event)"
          />
          @if (formError()) {
            <p class="text-sm text-danger" role="alert">{{ formError()! | transloco }}</p>
          }
          <kadr-button variant="primary" type="submit" [loading]="submitting()" class="w-full">
            {{ 'auth.forgot.submit' | transloco }}
          </kadr-button>
        </form>
      } @else {
        <div
          class="flex flex-col gap-4 rounded-md bg-info-soft px-4 py-4 text-start"
          role="status"
          data-testid="forgot-sent"
        >
          <div class="flex items-start gap-3">
            <span class="mt-0.5 text-info"><kadr-icon name="info" size="md" /></span>
            <div class="flex flex-col gap-1">
              <p class="text-sm font-bold text-fg">{{ 'auth.forgot.sentTitle' | transloco }}</p>
              <p class="text-sm text-fg">{{ 'auth.forgot.sentBody' | transloco }}</p>
            </div>
          </div>
          <button
            type="button"
            class="w-fit text-sm font-medium text-accent underline-offset-2 hover:underline focus-visible:outline-none"
            data-testid="forgot-again"
            (click)="again()"
          >
            {{ 'auth.forgot.again' | transloco }}
          </button>
        </div>
      }

      <div authFooter class="text-start text-sm">
        <a routerLink="/login" class="text-accent underline-offset-2 hover:underline">
          {{ 'auth.backToLogin' | transloco }}
        </a>
      </div>
    </kadr-auth-shell>
  `,
})
export class ForgotPasswordPage {
  private readonly api = inject(AuthApi);
  private readonly transloco = inject(TranslocoService);

  readonly state = signal<ForgotState>('form');
  readonly email = signal('');
  readonly submitting = signal(false);
  readonly emailError = signal<string | undefined>(undefined);
  readonly formError = signal<string | null>(null);

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.formError.set(null);
    const email = this.email().trim();
    if (!email || !email.includes('@')) {
      this.emailError.set(this.transloco.translate('ui.emailError'));
      return;
    }
    this.emailError.set(undefined);
    this.submitting.set(true);
    try {
      await this.api.forgotPassword({ email });
      // Same neutral screen whether or not an account exists.
      this.state.set('sent');
    } catch {
      this.formError.set('auth.forgot.failed');
    } finally {
      this.submitting.set(false);
    }
  }

  again(): void {
    this.state.set('form');
  }
}
