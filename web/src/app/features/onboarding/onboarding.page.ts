import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthProvidersService } from '../../core/auth/auth-providers.service';
import { AuthAlertComponent } from '../auth/auth-alert.component';
import { AuthShellLayout } from '../auth/auth-shell.layout';
import { GoogleSignInFlow, googleAlertKindForCode, GoogleAlertKind } from '../auth/google-sign-in.flow';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { GoogleSignInButtonComponent } from '../../shared/ui/google-sign-in-button/google-sign-in-button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { StepperComponent, StepperStep } from '../../shared/ui/stepper/stepper.component';
import {
  ONBOARDING_STEP_IDS,
  OnboardingStore,
} from './onboarding.store';

const DAY_CODES = ['sun', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat'] as const;

@Component({
  selector: 'kadr-onboarding-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    RouterLink,
    ButtonComponent,
    InputComponent,
    StepperComponent,
    AuthShellLayout,
    AuthAlertComponent,
    BadgeComponent,
    IconComponent,
    GoogleSignInButtonComponent,
  ],
  template: `
    <kadr-auth-shell
      [title]="'onboarding.title' | transloco"
      [subtitle]="'onboarding.subtitle' | transloco"
      [showSecureBadge]="false"
    >
      @if (!isDone()) {
        <kadr-stepper
          [steps]="stepperSteps()"
          [currentIndex]="store.draft().stepIndex"
          [ariaLabel]="'onboarding.stepsAria' | transloco"
        />
      }

      <section class="flex flex-col">
          @if (isDone()) {
            <div class="flex flex-col gap-4 text-start">
              <h2 class="text-xl font-bold text-fg">{{ 'onboarding.doneTitle' | transloco }}</h2>
              <p class="text-sm text-fg-2">
                {{ 'onboarding.doneBody' | transloco: { slug: store.draft().result?.slug } }}
              </p>
              @if (store.draft().googleVerified) {
                <p class="text-sm text-fg-2">{{ 'onboarding.google.doneHint' | transloco }}</p>
              } @else {
                <p class="text-sm text-fg-2">{{ 'onboarding.verifyReminder' | transloco }}</p>
              }
              <a routerLink="/login">
                <kadr-button variant="primary" type="button" class="w-full">
                  {{ 'onboarding.goToLogin' | transloco }}
                </kadr-button>
              </a>
            </div>
          } @else {
            @switch (currentStepId()) {
              @case ('account') {
                <div class="flex flex-col gap-4">
                  <h2 class="text-lg font-bold text-fg">{{ 'onboarding.steps.account' | transloco }}</h2>

                  @if (googleError(); as gErr) {
                    <kadr-auth-alert
                      [tone]="gErr === 'deactivated' ? 'danger' : 'warning'"
                      [icon]="gErr === 'deactivated' ? 'lock' : 'alert'"
                      [title]="('auth.googleAlert.' + gErr + '.title') | transloco"
                      [message]="('auth.googleAlert.' + gErr + '.body') | transloco"
                      data-testid="wizard-google-alert"
                    />
                  }

                  @if (store.draft().googleVerified) {
                    @if (store.googleExpired()) {
                      <div class="flex flex-col gap-3" data-testid="google-expired">
                        <kadr-auth-alert
                          tone="warning"
                          icon="alert"
                          [title]="'onboarding.google.expiredTitle' | transloco"
                          [message]="'onboarding.google.expiredBody' | transloco"
                        />
                        @if (googleEnabled()) {
                          <kadr-google-sign-in-button
                            text="continue_with"
                            [disabled]="googleBusy()"
                            (credential)="onGoogleCredential($event)"
                          />
                        }
                      </div>
                    } @else {
                      <div
                        class="flex items-start gap-3 rounded-md border border-line bg-subtle px-4 py-3 text-start"
                        data-testid="google-banner"
                      >
                        <span
                          class="inline-flex size-10 shrink-0 items-center justify-center rounded-md border border-line bg-surface"
                        >
                          <img src="/brand/google-g.svg" alt="" width="24" height="24" class="size-6" />
                        </span>
                        <div class="flex min-w-0 flex-1 flex-col gap-1">
                          <div class="flex flex-wrap items-center gap-2">
                            <p class="text-sm font-bold text-fg">{{ 'onboarding.google.bannerTitle' | transloco }}</p>
                            <kadr-badge tone="success" [label]="'onboarding.google.verifiedTag' | transloco" />
                          </div>
                          <p class="text-sm text-fg-2">{{ 'onboarding.google.bannerBody' | transloco }}</p>
                        </div>
                      </div>
                    }

                    <kadr-input
                      [label]="'onboarding.fields.fullName' | transloco"
                      autocomplete="name"
                      [value]="store.draft().fullName"
                      (valueChange)="store.patch({ fullName: $event })"
                    />

                    <div class="flex flex-col gap-2" data-testid="google-email">
                      <kadr-input
                        [label]="'ui.email' | transloco"
                        type="email"
                        [readOnly]="true"
                        [value]="store.draft().email"
                      />
                      <div class="flex flex-wrap items-center gap-2 text-xs text-fg-2">
                        <span
                          class="inline-flex items-center gap-1 rounded-full bg-subtle px-2.5 py-1 font-medium"
                        >
                          <kadr-icon name="lock" size="sm" />
                          {{ 'onboarding.google.accountBadge' | transloco }}
                        </span>
                        <span>{{ 'onboarding.google.emailVerified' | transloco }}</span>
                      </div>
                    </div>

                    <div class="flex flex-col gap-3 rounded-md border border-line px-4 py-3 text-start">
                      <button
                        type="button"
                        class="flex w-full items-start gap-3 text-start focus-visible:outline-none"
                        [attr.aria-expanded]="passwordOpen()"
                        aria-controls="google-password-section"
                        data-testid="google-password-toggle"
                        (click)="togglePassword()"
                      >
                        <span class="mt-0.5 text-fg-2"><kadr-icon name="key" size="md" /></span>
                        <span class="flex min-w-0 flex-1 flex-col gap-1">
                          <span class="text-sm font-bold text-fg">{{ 'onboarding.google.passwordTitle' | transloco }}</span>
                          <span class="text-sm text-fg-2">{{ 'onboarding.google.passwordBody' | transloco }}</span>
                        </span>
                        <span class="mt-0.5 text-fg-muted" [class.rotate-180]="passwordOpen()">
                          <kadr-icon name="chevron-down" size="md" />
                        </span>
                      </button>
                      @if (passwordOpen()) {
                        <div id="google-password-section" class="flex flex-col gap-4" data-testid="google-password-fields">
                          <kadr-input
                            [label]="'onboarding.google.newPassword' | transloco"
                            type="password"
                            autocomplete="new-password"
                            [revealable]="true"
                            [showLabel]="'auth.showPassword' | transloco"
                            [hideLabel]="'auth.hidePassword' | transloco"
                            [hint]="'onboarding.fields.passwordHint' | transloco"
                            [value]="store.draft().password"
                            (valueChange)="store.patch({ password: $event })"
                          />
                          <kadr-input
                            [label]="'onboarding.google.confirmPassword' | transloco"
                            type="password"
                            autocomplete="new-password"
                            [revealable]="true"
                            [showLabel]="'auth.showPassword' | transloco"
                            [hideLabel]="'auth.hidePassword' | transloco"
                            [value]="store.draft().passwordConfirm"
                            (valueChange)="store.patch({ passwordConfirm: $event })"
                          />
                        </div>
                      }
                    </div>

                    <button
                      type="button"
                      class="w-fit text-sm text-fg-2 underline-offset-2 hover:underline focus-visible:outline-none"
                      data-testid="google-discard"
                      (click)="discardGoogle()"
                    >
                      {{ 'onboarding.google.discard' | transloco }}
                    </button>
                  } @else {
                    @if (googleEnabled()) {
                      <div class="flex flex-col gap-3" data-testid="wizard-google-entry">
                        <kadr-google-sign-in-button
                          text="continue_with"
                          [disabled]="googleBusy()"
                          (credential)="onGoogleCredential($event)"
                        />
                        @if (googleBusy()) {
                          <p class="text-center text-sm text-fg-2" role="status">
                            {{ 'auth.google.verifying' | transloco }}
                          </p>
                        }
                        <div class="flex items-center gap-3 text-sm text-fg-muted" aria-hidden="true">
                          <span class="h-px flex-1 bg-line"></span>
                          <span>{{ 'auth.or' | transloco }}</span>
                          <span class="h-px flex-1 bg-line"></span>
                        </div>
                      </div>
                    }
                    <kadr-input
                      [label]="'onboarding.fields.fullName' | transloco"
                      autocomplete="name"
                      [value]="store.draft().fullName"
                      (valueChange)="store.patch({ fullName: $event })"
                    />
                    <kadr-input
                      [label]="'ui.email' | transloco"
                      type="email"
                      autocomplete="email"
                      [value]="store.draft().email"
                      (valueChange)="store.patch({ email: $event })"
                    />
                    <kadr-input
                      [label]="'auth.password' | transloco"
                      type="password"
                      autocomplete="new-password"
                      [value]="store.draft().password"
                      [hint]="'onboarding.fields.passwordHint' | transloco"
                      (valueChange)="store.patch({ password: $event })"
                    />
                  }
                </div>
              }
              @case ('company') {
                <div class="flex flex-col gap-4">
                  <h2 class="text-lg font-bold text-fg">{{ 'onboarding.steps.company' | transloco }}</h2>
                  <kadr-input
                    [label]="'onboarding.fields.companyName' | transloco"
                    [value]="store.draft().companyName"
                    (valueChange)="store.patch({ companyName: $event })"
                  />
                  <label class="grid max-w-md gap-2 text-sm">
                    <span class="font-medium text-fg">{{ 'onboarding.fields.country' | transloco }}</span>
                    <select
                      class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
                      [value]="store.draft().countryCode"
                      (change)="onCountry($event)"
                    >
                      <option value="EG">{{ 'onboarding.countries.EG' | transloco }}</option>
                      <option value="SA">{{ 'onboarding.countries.SA' | transloco }}</option>
                    </select>
                  </label>
                  <kadr-input
                    [label]="'onboarding.fields.timeZone' | transloco"
                    [value]="store.draft().timeZone"
                    (valueChange)="store.patch({ timeZone: $event })"
                  />
                  <label class="grid max-w-md gap-2 text-sm">
                    <span class="font-medium text-fg">{{ 'onboarding.fields.language' | transloco }}</span>
                    <select
                      class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
                      [value]="store.draft().defaultLanguage"
                      (change)="store.patch({ defaultLanguage: ($any($event.target).value) })"
                    >
                      <option value="ar">{{ 'uiKit.langAr' | transloco }}</option>
                      <option value="en">{{ 'uiKit.langEn' | transloco }}</option>
                    </select>
                  </label>
                  <p class="rounded-md border border-dashed border-line bg-app px-3 py-3 text-sm text-fg-2">
                    {{ 'onboarding.logoSkipHint' | transloco }}
                  </p>
                </div>
              }
              @case ('industry') {
                <div class="flex flex-col gap-4">
                  <h2 class="text-lg font-bold text-fg">{{ 'onboarding.steps.industry' | transloco }}</h2>
                  <p class="text-sm text-fg-2">{{ 'onboarding.industryIntro' | transloco }}</p>
                  @if (industriesError()) {
                    <p class="text-sm text-danger" role="alert">{{ industriesError()! | transloco }}</p>
                    <kadr-button variant="secondary" type="button" (click)="reloadIndustries()">
                      {{ 'home.retry' | transloco }}
                    </kadr-button>
                  } @else if (store.industries().length === 0) {
                    <p class="text-sm text-fg-muted">{{ 'ui.loading' | transloco }}</p>
                  } @else {
                    <div class="grid gap-3 sm:grid-cols-2">
                      @for (item of store.industries(); track item.code) {
                        <button
                          type="button"
                          class="rounded-md border px-4 py-3 text-start focus-visible:outline-none"
                          [class.border-accent]="store.draft().industryCode === item.code"
                          [class.bg-accent-soft]="store.draft().industryCode === item.code"
                          [class.border-line]="store.draft().industryCode !== item.code"
                          (click)="store.patch({ industryCode: item.code })"
                        >
                          <span class="block text-sm font-medium text-fg">
                            {{ lang() === 'en' ? item.nameEn : item.nameAr }}
                          </span>
                          <span class="mt-1 block text-xs text-fg-muted">{{ item.code }}</span>
                        </button>
                      }
                    </div>
                  }
                  <label class="flex items-start gap-3 text-sm text-fg">
                    <input
                      type="checkbox"
                      class="mt-1 size-4"
                      [checked]="store.draft().industryConfirmed"
                      (change)="store.patch({ industryConfirmed: ($any($event.target).checked) })"
                    />
                    <span>{{ 'onboarding.industryConfirm' | transloco }}</span>
                  </label>
                </div>
              }
              @case ('work') {
                <div class="flex flex-col gap-4">
                  <h2 class="text-lg font-bold text-fg">{{ 'onboarding.steps.work' | transloco }}</h2>
                  <fieldset>
                    <legend class="mb-2 text-sm font-medium text-fg">
                      {{ 'onboarding.fields.workDays' | transloco }}
                    </legend>
                    <div class="flex flex-wrap gap-2">
                      @for (day of dayCodes; track day) {
                        <label
                          class="inline-flex cursor-pointer items-center gap-2 rounded-md border border-line px-3 py-2 text-sm"
                          [class.bg-accent-soft]="store.draft().workWeekDays.includes(day)"
                        >
                          <input
                            type="checkbox"
                            class="size-4"
                            [checked]="store.draft().workWeekDays.includes(day)"
                            (change)="toggleDay('workWeekDays', day, $event)"
                          />
                          {{ ('onboarding.days.' + day) | transloco }}
                        </label>
                      }
                    </div>
                  </fieldset>
                  <fieldset>
                    <legend class="mb-2 text-sm font-medium text-fg">
                      {{ 'onboarding.fields.weekend' | transloco }}
                    </legend>
                    <div class="flex flex-wrap gap-2">
                      @for (day of dayCodes; track day) {
                        <label
                          class="inline-flex cursor-pointer items-center gap-2 rounded-md border border-line px-3 py-2 text-sm"
                          [class.bg-subtle]="store.draft().workWeekWeekend.includes(day)"
                        >
                          <input
                            type="checkbox"
                            class="size-4"
                            [checked]="store.draft().workWeekWeekend.includes(day)"
                            (change)="toggleDay('workWeekWeekend', day, $event)"
                          />
                          {{ ('onboarding.days.' + day) | transloco }}
                        </label>
                      }
                    </div>
                  </fieldset>
                  <div class="grid gap-4 sm:grid-cols-2">
                    <kadr-input
                      [label]="'onboarding.fields.workStart' | transloco"
                      [value]="store.draft().defaultWorkStart"
                      (valueChange)="store.patch({ defaultWorkStart: $event })"
                    />
                    <kadr-input
                      [label]="'onboarding.fields.workEnd' | transloco"
                      [value]="store.draft().defaultWorkEnd"
                      (valueChange)="store.patch({ defaultWorkEnd: $event })"
                    />
                  </div>
                </div>
              }
              @case ('branch') {
                <div class="flex flex-col gap-4">
                  <h2 class="text-lg font-bold text-fg">{{ 'onboarding.steps.branch' | transloco }}</h2>
                  <kadr-input
                    [label]="'onboarding.fields.branchName' | transloco"
                    [value]="store.draft().firstBranchName"
                    (valueChange)="store.patch({ firstBranchName: $event })"
                  />
                </div>
              }
              @case ('invite') {
                <div class="flex flex-col gap-4 text-start">
                  <h2 class="text-lg font-bold text-fg">{{ 'onboarding.steps.invite' | transloco }}</h2>
                  <p class="text-sm text-fg-2">{{ 'onboarding.inviteSkipBody' | transloco }}</p>
                </div>
              }
            }

            @if (stepError()) {
              <p class="mt-4 text-sm text-danger" role="alert">{{ stepError()! | transloco }}</p>
            }
            @if (store.error()) {
              <p class="mt-4 text-sm text-danger" role="alert">
                {{ store.error()! | transloco }}
              </p>
            }

            <div class="mt-6 flex flex-wrap items-center justify-between gap-3">
              <kadr-button
                variant="ghost"
                type="button"
                [disabled]="store.draft().stepIndex === 0 || store.submitting()"
                (click)="back()"
              >
                {{ 'onboarding.back' | transloco }}
              </kadr-button>
              <div class="flex flex-wrap gap-2">
                @if (currentStepId() === 'company' || currentStepId() === 'invite') {
                  <kadr-button variant="secondary" type="button" (click)="next()">
                    {{ 'ui.skip' | transloco }}
                  </kadr-button>
                }
                @if (currentStepId() === 'invite') {
                  <kadr-button
                    variant="primary"
                    type="button"
                    [loading]="store.submitting()"
                    (click)="finish()"
                  >
                    {{ 'onboarding.finish' | transloco }}
                  </kadr-button>
                } @else {
                  <kadr-button variant="primary" type="button" (click)="next()">
                    {{ 'onboarding.continue' | transloco }}
                  </kadr-button>
                }
              </div>
            </div>
          }
      </section>

      <div authFooter class="text-start text-sm text-fg-muted">
        <a routerLink="/login" class="text-accent underline-offset-2 hover:underline">
          {{ 'onboarding.haveAccount' | transloco }}
        </a>
      </div>
    </kadr-auth-shell>
  `,
})
export class OnboardingPage implements OnInit {
  protected readonly store = inject(OnboardingStore);
  private readonly transloco = inject(TranslocoService);
  private readonly providers = inject(AuthProvidersService);
  private readonly googleFlow = inject(GoogleSignInFlow);

  protected readonly googleEnabled = computed(() => this.providers.googleEnabled());
  protected readonly googleBusy = signal(false);
  protected readonly googleError = signal<GoogleAlertKind | null>(null);
  protected readonly passwordOpen = signal(false);

  protected readonly dayCodes = DAY_CODES;
  protected readonly stepError = signal<string | null>(null);
  protected readonly industriesError = signal<string | null>(null);
  protected readonly lang = computed(() => this.transloco.getActiveLang());

  protected readonly stepperSteps = computed<StepperStep[]>(() =>
    ONBOARDING_STEP_IDS.map((id) => ({
      id,
      label: `onboarding.steps.${id}`,
    })),
  );

  protected readonly currentStepId = computed(
    () => ONBOARDING_STEP_IDS[this.store.draft().stepIndex] ?? 'invite',
  );

  protected readonly isDone = computed(
    () => !!this.store.draft().result || this.store.draft().stepIndex >= ONBOARDING_STEP_IDS.length,
  );

  ngOnInit(): void {
    this.store.hydrate();
    this.passwordOpen.set(!!this.store.draft().password);
    void this.providers.load();
    void this.reloadIndustries();
  }

  async reloadIndustries(): Promise<void> {
    this.industriesError.set(null);
    try {
      await this.store.loadIndustries();
    } catch {
      this.industriesError.set('onboarding.errors.industries');
    }
  }

  async onGoogleCredential(credential: string): Promise<void> {
    if (this.googleBusy()) {
      return;
    }
    this.googleError.set(null);
    this.stepError.set(null);
    this.googleBusy.set(true);
    try {
      const result = await this.googleFlow.signIn(credential);
      if (result.kind === 'not_found') {
        // No account yet: continue the wizard with the Google-verified identity.
        this.store.startGoogleRegistration({
          token: result.registrationToken,
          email: result.email,
          fullName: result.name,
        });
        this.passwordOpen.set(false);
      } else if (result.kind === 'error') {
        this.googleError.set(googleAlertKindForCode(result.code));
      }
    } finally {
      this.googleBusy.set(false);
    }
  }

  togglePassword(): void {
    const open = !this.passwordOpen();
    this.passwordOpen.set(open);
    if (!open) {
      this.store.patch({ password: '', passwordConfirm: '' });
    }
  }

  discardGoogle(): void {
    this.googleError.set(null);
    this.stepError.set(null);
    this.passwordOpen.set(false);
    this.store.discardGoogleRegistration();
  }

  onCountry(event: Event): void {
    const code = (event.target as HTMLSelectElement).value;
    const patch =
      code === 'SA'
        ? {
            countryCode: code,
            timeZone: 'Asia/Riyadh',
            workWeekDays: ['sun', 'mon', 'tue', 'wed', 'thu'],
            workWeekWeekend: ['fri', 'sat'],
          }
        : {
            countryCode: code,
            timeZone: 'Africa/Cairo',
            workWeekDays: ['sun', 'mon', 'tue', 'wed', 'thu'],
            workWeekWeekend: ['fri', 'sat'],
          };
    this.store.patch(patch);
  }

  toggleDay(field: 'workWeekDays' | 'workWeekWeekend', day: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const current = [...this.store.draft()[field]];
    const next = checked
      ? [...new Set([...current, day])]
      : current.filter((d) => d !== day);
    this.store.patch({ [field]: next });
  }

  back(): void {
    this.stepError.set(null);
    this.store.setStepIndex(this.store.draft().stepIndex - 1);
  }

  next(): void {
    if (this.currentStepId() !== 'invite') {
      const validation = this.store.validateCurrentStep();
      if (validation) {
        this.stepError.set(validation);
        return;
      }
    }
    this.stepError.set(null);
    this.store.setStepIndex(this.store.draft().stepIndex + 1);
  }

  async finish(): Promise<void> {
    this.stepError.set(null);
    const invalid = this.store.firstInvalidStepIndex();
    if (invalid !== null) {
      this.store.setStepIndex(invalid);
      this.stepError.set(this.store.validateStep(invalid));
      return;
    }
    await this.store.submit();
  }
}
