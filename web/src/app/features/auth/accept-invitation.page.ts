import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Meta } from '@angular/platform-browser';
import { InvitationsApi, ValidateInvitationResponse } from '../../core/api/invitations.api';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { AuthShellLayout } from './auth-shell.layout';

@Component({
  selector: 'kadr-accept-invitation-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    RouterLink,
    ButtonComponent,
    InputComponent,
    SkeletonComponent,
    AuthShellLayout,
  ],
  template: `
    <kadr-auth-shell
      [title]="'auth.acceptInviteTitle' | transloco"
      [subtitle]="subtitle()"
    >
      @if (state() === 'loading') {
        <kadr-skeleton height="8rem" />
      } @else if (state() === 'invalid') {
        <p class="text-sm text-danger" role="alert">{{ 'auth.acceptInviteInvalid' | transloco }}</p>
        <a routerLink="/login" class="mt-4 inline-flex text-sm font-medium text-accent">{{ 'auth.acceptInviteGoLogin' | transloco }}</a>
      } @else if (state() === 'form' && info(); as inv) {
        <form class="flex flex-col gap-4" (submit)="onSubmit($event)">
          <p class="text-sm text-fg-2">
            {{ 'auth.acceptInviteFor' | transloco: { email: inv.email, company: inv.companyName, role: inv.roleName } }}
          </p>
          <kadr-input
            [label]="'onboarding.fields.fullName' | transloco"
            [value]="fullName()"
            [error]="nameError() ?? undefined"
            (valueChange)="fullName.set($event)"
          />
          <kadr-input
            [label]="'auth.password' | transloco"
            type="password"
            [value]="password()"
            [hint]="'onboarding.fields.passwordHint' | transloco"
            [error]="passwordError() ?? undefined"
            (valueChange)="password.set($event)"
          />
          @if (formError()) {
            <p class="text-sm text-danger" role="alert">{{ formError()! | transloco }}</p>
          }
          <kadr-button variant="primary" type="submit" [loading]="submitting()" class="w-full">
            {{ 'auth.acceptInviteSubmit' | transloco }}
          </kadr-button>
        </form>
      } @else if (state() === 'done') {
        <p class="text-sm text-fg">{{ 'auth.acceptInviteDone' | transloco }}</p>
        <a routerLink="/login" class="mt-4 inline-flex text-sm font-medium text-accent">{{ 'auth.acceptInviteGoLogin' | transloco }}</a>
      }
    </kadr-auth-shell>
  `,
})
export class AcceptInvitationPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(InvitationsApi);
  private readonly i18n = inject(TranslocoService);
  private readonly meta = inject(Meta);

  private token = '';
  private previousReferrer: string | null = null;

  protected readonly state = signal<'loading' | 'invalid' | 'form' | 'done'>('loading');
  protected readonly info = signal<ValidateInvitationResponse | null>(null);
  protected readonly fullName = signal('');
  protected readonly password = signal('');
  protected readonly nameError = signal<string | null>(null);
  protected readonly passwordError = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly subtitle = signal('');

  ngOnInit(): void {
    this.previousReferrer = this.meta.getTag('name="referrer"')?.content ?? null;
    this.meta.updateTag({ name: 'referrer', content: 'no-referrer' });

    this.token = this.route.snapshot.queryParamMap.get('token')?.trim() ?? '';
    // Never log the token.
    void this.validate();
  }

  ngOnDestroy(): void {
    if (this.previousReferrer) {
      this.meta.updateTag({ name: 'referrer', content: this.previousReferrer });
    } else {
      this.meta.removeTag('name="referrer"');
    }
  }

  private async validate(): Promise<void> {
    if (!this.token) {
      this.state.set('invalid');
      return;
    }
    try {
      const result = await this.api.validate(this.token);
      if (result.status !== 'valid') {
        this.state.set('invalid');
        return;
      }
      this.info.set(result);
      this.subtitle.set(
        this.i18n.translate('auth.acceptInviteSubtitle', { company: result.companyName }),
      );
      this.state.set('form');
    } catch {
      this.state.set('invalid');
    }
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.nameError.set(null);
    this.passwordError.set(null);
    this.formError.set(null);
    if (!this.fullName().trim()) {
      this.nameError.set(this.i18n.translate('settings.users.fullNameRequired'));
      return;
    }
    if (this.password().length < 10) {
      this.passwordError.set(this.i18n.translate('auth.password_too_short'));
      return;
    }
    this.submitting.set(true);
    try {
      await this.api.accept(this.token, this.fullName().trim(), this.password());
      this.state.set('done');
    } catch (e) {
      const code = problemDetailsFromError(e)?.code;
      this.formError.set(code ? `auth.${code.split('.').pop()}` : 'auth.acceptInviteFailed');
      if (code === 'auth.password_too_short' || code === 'auth.password_too_common') {
        this.passwordError.set(this.i18n.translate(code));
        this.formError.set(null);
      }
    } finally {
      this.submitting.set(false);
    }
  }
}
