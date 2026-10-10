import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthApi, problemDetailsFromError } from '../../core/api/auth.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { AuthShellLayout } from './auth-shell.layout';

@Component({
  selector: 'kadr-verify-email-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, ButtonComponent, InputComponent, AuthShellLayout],
  template: `
    <kadr-auth-shell
      [title]="'auth.verifyTitle' | transloco"
      [subtitle]="'auth.verifySubtitle' | transloco"
    >
      @if (success()) {
        <div class="flex flex-col gap-4">
          <p class="text-sm text-success" role="status">{{ 'auth.verifySuccess' | transloco }}</p>
          <a routerLink="/login" class="text-sm text-accent underline-offset-2 hover:underline">
            {{ 'auth.backToLogin' | transloco }}
          </a>
        </div>
      } @else {
        <form class="flex flex-col gap-4" (submit)="onSubmit($event)">
          <kadr-input
            [label]="'auth.verifyToken' | transloco"
            name="token"
            [value]="token()"
            [error]="tokenError()"
            (valueChange)="token.set($event)"
          />
          @if (formError()) {
            <p class="text-sm text-danger" role="alert">{{ formError() }}</p>
          }
          <kadr-button variant="primary" type="submit" [loading]="submitting()" class="w-full">
            {{ 'auth.verifySubmit' | transloco }}
          </kadr-button>
        </form>
      }

      <div authFooter class="text-start text-sm">
        <a routerLink="/login" class="text-accent underline-offset-2 hover:underline">
          {{ 'auth.backToLogin' | transloco }}
        </a>
      </div>
    </kadr-auth-shell>
  `,
})
export class VerifyEmailPage implements OnInit {
  private readonly authApi = inject(AuthApi);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);

  readonly token = signal('');
  readonly submitting = signal(false);
  readonly success = signal(false);
  readonly formError = signal<string | null>(null);
  readonly tokenError = signal<string | undefined>(undefined);

  ngOnInit(): void {
    const fromQuery = this.route.snapshot.queryParamMap.get('token');
    if (fromQuery) {
      this.token.set(fromQuery);
    }
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.formError.set(null);
    const token = this.token().trim();
    if (!token) {
      this.tokenError.set(this.transloco.translate('auth.verifyTokenRequired'));
      return;
    }
    this.tokenError.set(undefined);
    this.submitting.set(true);
    try {
      await this.authApi.verifyEmail({ token });
      this.success.set(true);
    } catch (error) {
      const problem = problemDetailsFromError(error);
      this.formError.set(
        problem?.detail ||
          this.transloco.translate(
            problem?.code === 'auth.invalid_token' ? 'auth.verifyInvalid' : 'auth.verifyFailed',
          ),
      );
    } finally {
      this.submitting.set(false);
    }
  }
}
