import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { MeApi } from '../../core/api/me.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { IconComponent, IconName } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { MIN_PASSWORD_LENGTH } from '../onboarding/onboarding.store';

export type PasswordDrawerMode = 'change' | 'add';
type RuleStatus = 'met' | 'unmet' | 'failed';

const INVALID_CREDENTIALS = 'auth.invalid_credentials';
const PASSWORD_TOO_SHORT = 'auth.password_too_short';
const PASSWORD_TOO_COMMON = 'auth.password_too_common';

/**
 * Right-side drawer. The mode comes from the account state (has a password = change,
 * no password = add); there is no mode switch inside the drawer.
 */
@Component({
  selector: 'kadr-password-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, DrawerComponent, InputComponent, ButtonComponent, IconComponent],
  template: `
    <kadr-drawer
      [open]="open()"
      [title]="(mode() === 'change' ? 'account.password.changeTitle' : 'account.password.addTitle') | transloco"
      [subtitle]="(mode() === 'change' ? 'account.password.changeSubtitle' : 'account.password.addSubtitle') | transloco"
      [closeLabel]="'account.password.close' | transloco"
      (closed)="close()"
    >
      <form class="flex flex-col gap-4" (submit)="onSubmit($event)" data-testid="password-form">
        @if (mode() === 'change') {
          <kadr-input
            [label]="'account.password.current' | transloco"
            type="password"
            name="currentPassword"
            autocomplete="current-password"
            [revealable]="true"
            [showLabel]="'auth.showPassword' | transloco"
            [hideLabel]="'auth.hidePassword' | transloco"
            [value]="current()"
            [error]="currentError()"
            (valueChange)="current.set($event)"
          />
        }
        <kadr-input
          [label]="'account.password.new' | transloco"
          type="password"
          name="newPassword"
          autocomplete="new-password"
          [revealable]="true"
          [showLabel]="'auth.showPassword' | transloco"
          [hideLabel]="'auth.hidePassword' | transloco"
          [value]="next()"
          [error]="nextError()"
          (valueChange)="next.set($event)"
        />
        <kadr-input
          [label]="'account.password.confirm' | transloco"
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

        <div class="rounded-md bg-subtle px-4 py-3 text-start" data-testid="password-rules">
          <p class="mb-2 text-xs font-bold text-fg-2">{{ 'account.password.rulesTitle' | transloco }}</p>
          <ul class="flex flex-col gap-2">
            @for (rule of rules(); track rule.key) {
              <li class="flex items-center gap-2 text-sm text-fg" [attr.data-status]="rule.status">
                <span [class]="rule.color"><kadr-icon [name]="rule.icon" size="sm" /></span>
                <span>{{ rule.label | transloco }}</span>
                <span class="sr-only">{{ ('account.password.status.' + rule.status) | transloco }}</span>
              </li>
            }
          </ul>
        </div>

        @if (formError()) {
          <p class="text-sm text-danger" role="alert">{{ formError()! | transloco }}</p>
        }
      </form>

      <div drawerFooter class="contents">
        <kadr-button variant="primary" type="button" [loading]="saving()" (click)="save()">
          {{ 'account.password.save' | transloco }}
        </kadr-button>
        <kadr-button variant="secondary" type="button" [disabled]="saving()" (click)="close()">
          {{ 'ui.cancel' | transloco }}
        </kadr-button>
      </div>
    </kadr-drawer>
  `,
})
export class PasswordDrawerComponent {
  private readonly me = inject(MeApi);
  private readonly transloco = inject(TranslocoService);

  readonly open = input(false);
  readonly mode = input<PasswordDrawerMode>('change');
  readonly closed = output<void>();
  readonly saved = output<PasswordDrawerMode>();

  protected readonly current = signal('');
  protected readonly next = signal('');
  protected readonly confirm = signal('');
  protected readonly saving = signal(false);
  protected readonly currentError = signal<string | undefined>(undefined);
  protected readonly nextError = signal<string | undefined>(undefined);
  protected readonly confirmError = signal<string | undefined>(undefined);
  protected readonly formError = signal<string | null>(null);
  private readonly commonRejected = signal(false);

  protected readonly rules = computed<
    { key: string; label: string; status: RuleStatus; icon: IconName; color: string }[]
  >(() => {
    const lengthStatus: RuleStatus = this.next().length >= MIN_PASSWORD_LENGTH ? 'met' : 'unmet';
    // "Not a common password" can only be checked by the server, so it stays neutral until then.
    const commonStatus: RuleStatus = this.commonRejected() ? 'failed' : 'unmet';
    const view = (key: string, label: string, status: RuleStatus) => ({
      key,
      label,
      status,
      icon: (status === 'met' ? 'check' : status === 'failed' ? 'x' : 'circle') as IconName,
      color:
        status === 'met' ? 'text-success' : status === 'failed' ? 'text-danger' : 'text-fg-muted',
    });
    return [
      view('length', 'account.password.ruleLength', lengthStatus),
      view('common', 'account.password.ruleCommon', commonStatus),
    ];
  });

  constructor() {
    // Start every opening from a clean form.
    effect(() => {
      if (this.open()) {
        untracked(() => this.reset());
      }
    });
  }

  close(): void {
    if (!this.saving()) {
      this.closed.emit();
    }
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    void this.save();
  }

  async save(): Promise<void> {
    if (this.saving()) {
      return;
    }
    this.clearErrors();
    const change = this.mode() === 'change';
    let invalid = false;
    if (change && !this.current()) {
      this.currentError.set(this.transloco.translate('account.password.currentRequired'));
      invalid = true;
    }
    if (this.next().length < MIN_PASSWORD_LENGTH) {
      this.nextError.set(this.transloco.translate(PASSWORD_TOO_SHORT));
      invalid = true;
    }
    if (this.next() !== this.confirm()) {
      this.confirmError.set(this.transloco.translate('auth.passwordMismatch'));
      invalid = true;
    }
    if (invalid) {
      return;
    }

    this.saving.set(true);
    try {
      await this.me.setPassword({
        ...(change ? { currentPassword: this.current() } : {}),
        newPassword: this.next(),
      });
      this.saved.emit(this.mode());
      this.reset();
    } catch (error) {
      const code = problemDetailsFromError(error)?.code;
      if (code === INVALID_CREDENTIALS) {
        this.currentError.set(this.transloco.translate('account.password.currentWrong'));
      } else if (code === PASSWORD_TOO_SHORT) {
        this.nextError.set(this.transloco.translate(PASSWORD_TOO_SHORT));
      } else if (code === PASSWORD_TOO_COMMON) {
        this.commonRejected.set(true);
        this.nextError.set(this.transloco.translate(PASSWORD_TOO_COMMON));
      } else {
        this.formError.set('account.password.failed');
      }
    } finally {
      this.saving.set(false);
    }
  }

  private clearErrors(): void {
    this.currentError.set(undefined);
    this.nextError.set(undefined);
    this.confirmError.set(undefined);
    this.formError.set(null);
    this.commonRejected.set(false);
  }

  private reset(): void {
    this.current.set('');
    this.next.set('');
    this.confirm.set('');
    this.clearErrors();
  }
}
