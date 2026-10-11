import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { MeApi } from '../../core/api/me.api';
import { SignInMethodsResponse } from '../../core/api/me.models';
import { AuthProvidersService } from '../../core/auth/auth-providers.service';
import { AuthService } from '../../core/auth/auth.service';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { GoogleSignInButtonComponent } from '../../shared/ui/google-sign-in-button/google-sign-in-button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { AuthAlertComponent } from '../auth/auth-alert.component';
import { PasswordDrawerComponent, PasswordDrawerMode } from './password-drawer.component';

type PageState = 'loading' | 'ready' | 'error';

const GOOGLE_ALREADY_LINKED = 'auth.google_already_linked';
const GOOGLE_EMAIL_UNVERIFIED = 'auth.google_email_unverified';
const LAST_SIGN_IN_METHOD = 'auth.last_sign_in_method';

@Component({
  selector: 'kadr-account-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    PageHeaderComponent,
    BadgeComponent,
    ButtonComponent,
    ConfirmDialogComponent,
    EmptyStateComponent,
    GoogleSignInButtonComponent,
    IconComponent,
    SkeletonComponent,
    AuthAlertComponent,
    PasswordDrawerComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-xl flex-col gap-4">
      <kadr-page-header
        [title]="'account.title' | transloco"
        [subtitle]="'account.subtitle' | transloco"
      />

      @switch (state()) {
        @case ('loading') {
          <div
            class="flex flex-col gap-4"
            role="status"
            [attr.aria-label]="'ui.loading' | transloco"
            data-testid="account-loading"
          >
            <kadr-skeleton height="7rem" />
            <kadr-skeleton height="7rem" />
          </div>
        }
        @case ('error') {
          <kadr-empty-state
            [title]="'account.loadErrorTitle' | transloco"
            [description]="'account.loadErrorBody' | transloco"
          >
            <kadr-button variant="secondary" type="button" (click)="load()">
              {{ 'home.retry' | transloco }}
            </kadr-button>
          </kadr-empty-state>
        }
        @default {
          @if (methods(); as m) {
            @if (actionError()) {
              <kadr-auth-alert
                tone="warning"
                icon="alert"
                [title]="'account.errors.title' | transloco"
                [message]="actionError()! | transloco"
                data-testid="account-error"
              />
            }

            <section
              class="flex flex-col gap-4 rounded-lg border border-line bg-surface p-5 shadow-sm"
              [attr.aria-label]="'account.password.cardTitle' | transloco"
              data-testid="password-card"
            >
              <div class="flex items-start gap-4">
                <span
                  class="inline-flex size-11 shrink-0 items-center justify-center rounded-md bg-subtle text-fg-2"
                >
                  <kadr-icon name="key" size="md" />
                </span>
                <div class="flex min-w-0 flex-1 flex-col gap-1 text-start">
                  <div class="flex flex-wrap items-center gap-2">
                    <h2 class="text-base font-bold text-fg">{{ 'account.password.cardTitle' | transloco }}</h2>
                    <kadr-badge
                      [tone]="m.password.enabled ? 'success' : 'neutral'"
                      [label]="(m.password.enabled ? 'account.status.enabled' : 'account.status.notEnabled') | transloco"
                    />
                  </div>
                  <p class="break-all text-sm text-fg-2"><bdi>{{ email() }}</bdi></p>
                  <p class="text-sm text-fg-muted">
                    {{ (m.password.enabled ? 'account.password.enabledHint' : 'account.password.notEnabledHint') | transloco }}
                  </p>
                </div>
              </div>
              <div>
                <kadr-button variant="secondary" type="button" (click)="openPasswordDrawer()">
                  <kadr-icon name="key" size="sm" />
                  {{ (m.password.enabled ? 'account.password.change' : 'account.password.add') | transloco }}
                </kadr-button>
              </div>
            </section>

            @if (googleEnabled()) {
              <section
                class="flex flex-col gap-4 rounded-lg border border-line bg-surface p-5 shadow-sm"
                [attr.aria-label]="'account.google.cardTitle' | transloco"
                data-testid="google-card"
              >
                <div class="flex items-start gap-4">
                  <span
                    class="inline-flex size-11 shrink-0 items-center justify-center rounded-md bg-subtle"
                  >
                    <img src="/brand/google-g.svg" alt="" width="24" height="24" class="size-6" />
                  </span>
                  <div class="flex min-w-0 flex-1 flex-col gap-1 text-start">
                    <div class="flex flex-wrap items-center gap-2">
                      <h2 class="text-base font-bold text-fg">{{ 'account.google.cardTitle' | transloco }}</h2>
                      <kadr-badge
                        [tone]="m.google.linked ? 'success' : 'neutral'"
                        [label]="(m.google.linked ? 'account.status.enabled' : 'account.status.notLinked') | transloco"
                      />
                    </div>
                    @if (m.google.linked) {
                      <p class="break-all text-sm text-fg-2"><bdi>{{ m.google.email }}</bdi></p>
                    } @else {
                      <p class="text-sm text-fg-muted">{{ 'account.google.notLinkedHint' | transloco }}</p>
                    }
                  </div>
                </div>

                @if (m.google.linked) {
                  <div class="flex flex-col gap-2">
                    <span
                      class="w-fit"
                      [attr.title]="m.password.enabled ? null : ('account.google.lastMethodHint' | transloco)"
                    >
                      <kadr-button
                        variant="danger-soft"
                        type="button"
                        [disabled]="!m.password.enabled"
                        (click)="confirmDisconnect.set(true)"
                      >
                        <kadr-icon name="unlink" size="sm" />
                        {{ 'account.google.disconnect' | transloco }}
                      </kadr-button>
                    </span>
                    @if (!m.password.enabled) {
                      <p class="text-xs text-fg-muted" data-testid="last-method-hint">
                        {{ 'account.google.lastMethodHint' | transloco }}
                      </p>
                    }
                  </div>
                } @else {
                  <div data-testid="google-link">
                    <kadr-google-sign-in-button
                      text="continue_with"
                      [disabled]="linking()"
                      (credential)="onLinkCredential($event)"
                    />
                  </div>
                }
              </section>
            }

            <p
              class="flex items-start gap-2 rounded-md bg-subtle px-4 py-3 text-start text-sm text-fg-2"
              data-testid="account-footer-note"
            >
              <span class="mt-0.5"><kadr-icon name="info" size="sm" /></span>
              {{ 'account.footerNote' | transloco }}
            </p>

            <kadr-password-drawer
              [open]="drawerOpen()"
              [mode]="m.password.enabled ? 'change' : 'add'"
              (closed)="drawerOpen.set(false)"
              (saved)="onPasswordSaved($event)"
            />

            <kadr-confirm-dialog
              [open]="confirmDisconnect()"
              [title]="'account.disconnect.title' | transloco"
              [message]="'account.disconnect.message' | transloco"
              [confirmLabel]="'account.google.disconnect' | transloco"
              [cancelLabel]="'ui.cancel' | transloco"
              [danger]="true"
              [busy]="disconnecting()"
              (confirmed)="disconnect()"
              (cancelled)="confirmDisconnect.set(false)"
            />
          }
        }
      }
    </div>
  `,
})
export class AccountPage implements OnInit {
  private readonly me = inject(MeApi);
  private readonly auth = inject(AuthService);
  private readonly providers = inject(AuthProvidersService);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly googleButton = viewChild(GoogleSignInButtonComponent);

  readonly state = signal<PageState>('loading');
  readonly methods = signal<SignInMethodsResponse | null>(null);
  readonly drawerOpen = signal(false);
  readonly confirmDisconnect = signal(false);
  readonly disconnecting = signal(false);
  readonly linking = signal(false);
  readonly actionError = signal<string | null>(null);

  readonly googleEnabled = computed(() => this.providers.googleEnabled());
  readonly email = computed(() => this.auth.me()?.email ?? '');

  ngOnInit(): void {
    void this.providers.load();
    void this.load();
  }

  async load(): Promise<void> {
    this.state.set('loading');
    try {
      this.methods.set(await this.me.getSignInMethods());
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  openPasswordDrawer(): void {
    this.actionError.set(null);
    this.drawerOpen.set(true);
  }

  async onPasswordSaved(mode: PasswordDrawerMode): Promise<void> {
    this.drawerOpen.set(false);
    this.toast.show(
      this.transloco.translate(
        mode === 'change' ? 'account.password.changed' : 'account.password.added',
      ),
      'success',
    );
    await this.reloadQuietly();
  }

  async onLinkCredential(credential: string): Promise<void> {
    if (this.linking()) {
      return;
    }
    this.actionError.set(null);
    this.linking.set(true);
    try {
      await this.me.linkGoogle(credential);
      this.toast.show(this.transloco.translate('account.google.linked'), 'success');
      await this.reloadQuietly();
    } catch (error) {
      const code = problemDetailsFromError(error)?.code;
      this.actionError.set(
        code === GOOGLE_ALREADY_LINKED
          ? 'account.errors.alreadyLinked'
          : code === GOOGLE_EMAIL_UNVERIFIED
            ? 'account.errors.googleUnverified'
            : 'account.errors.linkFailed',
      );
      void this.googleButton()?.refresh();
    } finally {
      this.linking.set(false);
    }
  }

  async disconnect(): Promise<void> {
    if (this.disconnecting()) {
      return;
    }
    this.disconnecting.set(true);
    this.actionError.set(null);
    try {
      await this.me.unlinkGoogle();
      this.confirmDisconnect.set(false);
      this.toast.show(this.transloco.translate('account.google.unlinked'), 'success');
      await this.reloadQuietly();
    } catch (error) {
      this.confirmDisconnect.set(false);
      this.actionError.set(
        problemDetailsFromError(error)?.code === LAST_SIGN_IN_METHOD
          ? 'account.errors.lastMethod'
          : 'account.errors.unlinkFailed',
      );
    } finally {
      this.disconnecting.set(false);
    }
  }

  /** Refresh the cards without flashing the skeleton; keeps the old data if the reload fails. */
  private async reloadQuietly(): Promise<void> {
    try {
      this.methods.set(await this.me.getSignInMethods());
    } catch {
      this.state.set('error');
    }
  }
}
