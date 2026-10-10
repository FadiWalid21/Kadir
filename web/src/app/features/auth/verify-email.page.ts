import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthApi, problemDetailsFromError } from '../../core/api/auth.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { InputComponent } from '../../shared/ui/input/input.component';

@Component({
  selector: 'kadr-verify-email-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, ButtonComponent, InputComponent],
  template: `
    <main class="flex min-h-dvh flex-col items-center justify-center bg-app px-4 py-10">
      <div class="w-full max-w-md rounded-lg border border-line bg-surface p-6 shadow-sm sm:p-8">
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
              {{ 'auth.verifyTitle' | transloco }}
            </h1>
            <p class="text-sm text-fg-2">{{ 'auth.verifySubtitle' | transloco }}</p>
          </div>
        </header>

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
          <p class="mt-6 text-center text-sm">
            <a routerLink="/login" class="text-accent underline-offset-2 hover:underline">
              {{ 'auth.backToLogin' | transloco }}
            </a>
          </p>
        }
      </div>
    </main>
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
