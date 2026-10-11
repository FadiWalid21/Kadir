import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { AuthService } from '../../core/auth/auth.service';
import { PlatformAuthService } from '../../core/platform/platform-auth.service';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';

type AlertKind = 'invalid' | 'locked' | 'failed' | null;

@Component({
  selector: 'kadr-platform-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    IconComponent,
    EmptyStateComponent,
  ],
  template: `
    @if (notAuthorized()) {
      <div class="flex min-h-dvh flex-col items-center justify-center bg-app px-4 py-10">
        <kadr-empty-state
          [title]="'platform.login.notAuthorizedTitle' | transloco"
          [description]="'platform.login.notAuthorizedBody' | transloco"
        >
          <a routerLink="/login" class="contents">
            <kadr-button variant="primary" type="button">
              {{ 'platform.login.goTenantLogin' | transloco }}
            </kadr-button>
          </a>
        </kadr-empty-state>
      </div>
    } @else {
      <div class="flex min-h-dvh flex-col items-center justify-center bg-app px-4 py-10">
        <div class="mb-6 flex flex-col items-center gap-2 text-center">
          <picture>
            <source type="image/webp" srcset="/brand/logo-kadr-128.webp" />
            <img
              src="/brand/logo-kadr-128.png"
              alt=""
              width="48"
              height="48"
              class="size-12 object-contain"
            />
          </picture>
          <p class="text-2xl font-bold text-fg">{{ 'platform.brand' | transloco }}</p>
          <p class="text-xs text-fg-muted">{{ 'platform.consoleLabel' | transloco }}</p>
        </div>

        <div
          class="w-full max-w-md rounded-lg border border-line bg-surface p-6 shadow-sm"
          data-testid="platform-login-card"
        >
          <div class="mb-3 flex justify-center">
            <span
              class="inline-flex items-center gap-2 rounded-full bg-subtle px-3 py-1 text-xs font-medium text-fg-2"
            >
              <span class="size-1.5 rounded-full bg-accent" aria-hidden="true"></span>
              {{ 'platform.login.badge' | transloco }}
            </span>
          </div>
          <h1 class="text-center text-xl font-bold text-fg">{{ 'platform.login.title' | transloco }}</h1>
          <p class="mt-2 text-center text-sm text-fg-2">{{ 'platform.login.subtitle' | transloco }}</p>

          <form class="mt-6 flex flex-col gap-4" (submit)="onSubmit($event)">
            <kadr-input
              [label]="'platform.login.email' | transloco"
              type="email"
              name="email"
              autocomplete="username"
              [value]="email()"
              [error]="emailError()"
              (valueChange)="email.set($event); clearError()"
            />
            <kadr-input
              [label]="'platform.login.password' | transloco"
              type="password"
              name="password"
              autocomplete="current-password"
              [revealable]="true"
              [showLabel]="'auth.showPassword' | transloco"
              [hideLabel]="'auth.hidePassword' | transloco"
              [value]="password()"
              [error]="passwordError()"
              (valueChange)="password.set($event); clearError()"
            />

            @if (alert() === 'invalid' || alert() === 'failed') {
              <p
                class="flex items-start gap-2 rounded-md bg-danger-soft px-3 py-2 text-sm text-danger"
                role="alert"
              >
                <kadr-icon name="alert" size="sm" />
                <span>{{
                  (alert() === 'invalid' ? 'platform.login.invalid' : 'platform.login.failed')
                    | transloco
                }}</span>
              </p>
            }
            @if (alert() === 'locked') {
              <p
                class="flex items-start gap-2 rounded-md bg-warning-soft px-3 py-2 text-sm text-warning"
                role="alert"
              >
                <kadr-icon name="lock" size="sm" />
                <span>{{ 'platform.login.locked' | transloco }}</span>
              </p>
            }

            <kadr-button variant="primary" type="submit" class="w-full" [loading]="busy()">
              {{ 'platform.login.submit' | transloco }}
            </kadr-button>
          </form>

          <p class="mt-4 flex items-start justify-center gap-2 text-center text-xs text-fg-muted">
            <kadr-icon name="shield" size="sm" class="mt-0.5 shrink-0" />
            <span>{{ 'platform.login.disclaimer' | transloco }}</span>
          </p>
        </div>
      </div>
    }
  `,
})
export class PlatformLoginPage implements OnInit {
  private readonly auth = inject(PlatformAuthService);
  private readonly tenantAuth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly emailError = signal<string | undefined>(undefined);
  protected readonly passwordError = signal<string | undefined>(undefined);
  protected readonly alert = signal<AlertKind>(null);
  protected readonly busy = signal(false);
  protected readonly notAuthorized = signal(false);

  ngOnInit(): void {
    void this.tenantAuth.restoreSession().then(() => {
      if (this.tenantAuth.status() === 'authenticated') {
        this.notAuthorized.set(true);
      }
    });
  }

  clearError(): void {
    this.alert.set(null);
    this.emailError.set(undefined);
    this.passwordError.set(undefined);
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    if (this.busy()) {
      return;
    }
    this.clearError();
    if (!this.email().trim()) {
      this.emailError.set('platform.login.emailRequired');
      return;
    }
    if (!this.password()) {
      this.passwordError.set('platform.login.passwordRequired');
      return;
    }

    this.busy.set(true);
    try {
      await this.auth.login(this.email().trim(), this.password());
      await this.router.navigateByUrl('/platform/companies');
    } catch (error) {
      const code = problemDetailsFromError(error)?.code;
      if (code === 'auth.account_locked') {
        this.alert.set('locked');
      } else if (code === 'auth.invalid_credentials') {
        this.alert.set('invalid');
      } else {
        this.alert.set('failed');
      }
    } finally {
      this.busy.set(false);
    }
  }
}
