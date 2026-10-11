import { inject, Injectable, signal } from '@angular/core';
import { AuthApi, problemDetailsFromError } from '../../core/api/auth.api';
import { RegisterTenantRequest, RegisterTenantResponse } from '../../core/api/auth.models';
import { IndustryDto, TemplatesApi } from '../../core/api/templates.api';

export const ONBOARDING_DRAFT_KEY = 'kadr.onboarding.draft';

/** Mirrors Kadr.SharedKernel AuthConstants.GoogleRegistrationLifetime (30 minutes). */
export const GOOGLE_REGISTRATION_LIFETIME_MS = 30 * 60 * 1000;
export const MIN_PASSWORD_LENGTH = 10;

const GOOGLE_REGISTRATION_INVALID = 'auth.google_registration_invalid';

export type OnboardingStepId =
  | 'account'
  | 'company'
  | 'industry'
  | 'work'
  | 'branch'
  | 'invite'
  | 'done';

export interface OnboardingDraft {
  stepIndex: number;
  fullName: string;
  email: string;
  password: string;
  /** Confirmation for the optional password in the Google variant. Never persisted. */
  passwordConfirm: string;
  /** True when step 1 came from Google (identity verified by Google). */
  googleVerified: boolean;
  /** One-time token from the API. Kept in memory only; never written to storage. */
  googleRegistrationToken: string | null;
  /** Epoch ms when the token was issued (30 minute lifetime). */
  googleRegisteredAt: number | null;
  companyName: string;
  countryCode: string;
  timeZone: string;
  defaultLanguage: string;
  industryCode: string;
  industryConfirmed: boolean;
  workWeekDays: string[];
  workWeekWeekend: string[];
  defaultWorkStart: string;
  defaultWorkEnd: string;
  firstBranchName: string;
  idempotencyKey: string | null;
  result: RegisterTenantResponse | null;
}

const DEFAULT_DRAFT: OnboardingDraft = {
  stepIndex: 0,
  fullName: '',
  email: '',
  password: '',
  passwordConfirm: '',
  googleVerified: false,
  googleRegistrationToken: null,
  googleRegisteredAt: null,
  companyName: '',
  countryCode: 'EG',
  timeZone: 'Africa/Cairo',
  defaultLanguage: 'ar',
  industryCode: '',
  industryConfirmed: false,
  workWeekDays: ['sun', 'mon', 'tue', 'wed', 'thu'],
  workWeekWeekend: ['fri', 'sat'],
  defaultWorkStart: '09:00',
  defaultWorkEnd: '17:00',
  firstBranchName: '',
  idempotencyKey: null,
  result: null,
};

export const ONBOARDING_STEP_IDS: OnboardingStepId[] = [
  'account',
  'company',
  'industry',
  'work',
  'branch',
  'invite',
];

@Injectable({ providedIn: 'root' })
export class OnboardingStore {
  private readonly authApi = inject(AuthApi);
  private readonly templatesApi = inject(TemplatesApi);

  private readonly draftSignal = signal<OnboardingDraft>({ ...DEFAULT_DRAFT });
  private readonly industriesSignal = signal<IndustryDto[]>([]);
  private readonly submittingSignal = signal(false);
  private readonly errorSignal = signal<string | null>(null);
  private readonly googleExpiredSignal = signal(false);
  private googleExpiryTimer: ReturnType<typeof setTimeout> | null = null;

  /** True when a Google confirmation existed but is older than 30 minutes (or was lost on reload). */
  readonly googleExpired = this.googleExpiredSignal.asReadonly();
  readonly draft = this.draftSignal.asReadonly();
  readonly industries = this.industriesSignal.asReadonly();
  readonly submitting = this.submittingSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

  hydrate(): void {
    // A live in-memory Google confirmation is newer than anything in storage (it is never stored).
    if (this.draftSignal().googleRegistrationToken) {
      return;
    }
    try {
      const raw = sessionStorage.getItem(ONBOARDING_DRAFT_KEY);
      if (!raw) {
        return;
      }
      const parsed = JSON.parse(raw) as Partial<OnboardingDraft>;
      this.draftSignal.set({
        ...DEFAULT_DRAFT,
        ...parsed,
        password: parsed.password ?? '',
        passwordConfirm: '',
        googleRegistrationToken: null,
      });
      // The token is not stored, so a Google draft restored from storage needs a new confirmation.
      if (this.draftSignal().googleVerified) {
        this.markGoogleExpired();
      }
    } catch {
      sessionStorage.removeItem(ONBOARDING_DRAFT_KEY);
    }
  }

  persist(): void {
    // The one-time token and the password confirmation are never written to storage.
    const persistable = {
      ...this.draftSignal(),
      googleRegistrationToken: null,
      passwordConfirm: '',
    };
    sessionStorage.setItem(ONBOARDING_DRAFT_KEY, JSON.stringify(persistable));
  }

  clear(): void {
    sessionStorage.removeItem(ONBOARDING_DRAFT_KEY);
    this.clearGoogleTimer();
    this.googleExpiredSignal.set(false);
    this.draftSignal.set({ ...DEFAULT_DRAFT });
    this.errorSignal.set(null);
  }

  /**
   * Switches step 1 to the Google variant. The rest of the draft is kept.
   * Name is only prefilled when empty so an edited name survives a second confirmation.
   */
  startGoogleRegistration(input: { token: string; email: string | null; fullName: string | null }): void {
    this.clearGoogleTimer();
    this.googleExpiredSignal.set(false);
    this.errorSignal.set(null);
    const current = this.draftSignal();
    this.patch({
      googleVerified: true,
      googleRegistrationToken: input.token,
      googleRegisteredAt: Date.now(),
      email: input.email ?? current.email,
      fullName: current.fullName.trim() ? current.fullName : (input.fullName ?? ''),
      password: '',
      passwordConfirm: '',
      stepIndex: 0,
      idempotencyKey: null,
    });
    this.googleExpiryTimer = setTimeout(
      () => this.markGoogleExpired(),
      GOOGLE_REGISTRATION_LIFETIME_MS,
    );
  }

  /** Leaves the Google variant (wrong account chosen): step 1 becomes the normal email + password form. */
  discardGoogleRegistration(): void {
    this.clearGoogleTimer();
    this.googleExpiredSignal.set(false);
    this.patch({
      googleVerified: false,
      googleRegistrationToken: null,
      googleRegisteredAt: null,
      email: '',
      password: '',
      passwordConfirm: '',
      idempotencyKey: null,
    });
  }

  /** Detects expiry by clock and returns whether the Google confirmation is expired. */
  checkGoogleExpiry(): boolean {
    const d = this.draftSignal();
    if (!d.googleVerified) {
      return false;
    }
    const age = d.googleRegisteredAt === null ? Infinity : Date.now() - d.googleRegisteredAt;
    if (!d.googleRegistrationToken || age >= GOOGLE_REGISTRATION_LIFETIME_MS) {
      this.markGoogleExpired();
    }
    return this.googleExpiredSignal();
  }

  private markGoogleExpired(): void {
    this.clearGoogleTimer();
    this.googleExpiredSignal.set(true);
    this.draftSignal.update((d) => ({ ...d, googleRegistrationToken: null, stepIndex: 0 }));
  }

  private clearGoogleTimer(): void {
    if (this.googleExpiryTimer) {
      clearTimeout(this.googleExpiryTimer);
      this.googleExpiryTimer = null;
    }
  }

  patch(partial: Partial<OnboardingDraft>): void {
    this.draftSignal.update((d) => ({ ...d, ...partial }));
    this.persist();
  }

  setStepIndex(index: number): void {
    this.patch({ stepIndex: Math.max(0, Math.min(index, ONBOARDING_STEP_IDS.length)) });
  }

  async loadIndustries(): Promise<void> {
    if (this.industriesSignal().length > 0) {
      return;
    }
    this.industriesSignal.set(await this.templatesApi.listIndustries());
  }

  validateStep(stepIndex: number = this.draftSignal().stepIndex): string | null {
    const d = this.draftSignal();
    const step = ONBOARDING_STEP_IDS[stepIndex];
    switch (step) {
      case 'account':
        if (!d.fullName.trim()) {
          return 'onboarding.errors.fullName';
        }
        if (d.googleVerified) {
          if (this.checkGoogleExpiry()) {
            return 'onboarding.errors.googleExpired';
          }
          // Password is optional with Google, but when given it follows the same rule and must match.
          if (d.password.length > 0) {
            if (d.password.length < MIN_PASSWORD_LENGTH) {
              return 'onboarding.errors.password';
            }
            if (d.password !== d.passwordConfirm) {
              return 'onboarding.errors.passwordMismatch';
            }
          }
          return null;
        }
        if (!d.email.includes('@')) {
          return 'onboarding.errors.email';
        }
        if (d.password.length < MIN_PASSWORD_LENGTH) {
          return 'onboarding.errors.password';
        }
        return null;
      case 'company':
        if (!d.companyName.trim()) {
          return 'onboarding.errors.companyName';
        }
        return null;
      case 'industry':
        if (!d.industryCode) {
          return 'onboarding.errors.industry';
        }
        if (!d.industryConfirmed) {
          return 'onboarding.errors.industryConfirm';
        }
        return null;
      case 'work':
        if (d.workWeekDays.length === 0) {
          return 'onboarding.errors.workDays';
        }
        if (!/^\d{2}:\d{2}$/.test(d.defaultWorkStart) || !/^\d{2}:\d{2}$/.test(d.defaultWorkEnd)) {
          return 'onboarding.errors.workHours';
        }
        return null;
      case 'branch':
        if (!d.firstBranchName.trim()) {
          return 'onboarding.errors.branchName';
        }
        return null;
      default:
        return null;
    }
  }

  validateCurrentStep(): string | null {
    return this.validateStep(this.draftSignal().stepIndex);
  }

  firstInvalidStepIndex(): number | null {
    for (let i = 0; i < ONBOARDING_STEP_IDS.length - 1; i++) {
      if (this.validateStep(i)) {
        return i;
      }
    }
    return null;
  }

  toRegisterBody(): RegisterTenantRequest {
    const d = this.draftSignal();
    const identity: Pick<RegisterTenantRequest, 'email' | 'password' | 'googleRegistrationToken'> =
      d.googleVerified && d.googleRegistrationToken
        ? {
            // The identity email comes from the token on the server; it is never sent from the client.
            googleRegistrationToken: d.googleRegistrationToken,
            ...(d.password ? { password: d.password } : {}),
          }
        : { email: d.email.trim(), password: d.password };
    return {
      fullName: d.fullName.trim(),
      ...identity,
      companyName: d.companyName.trim(),
      industryCode: d.industryCode,
      timeZone: d.timeZone,
      defaultLanguage: d.defaultLanguage,
      countryCode: d.countryCode,
      workWeekDays: d.workWeekDays,
      workWeekWeekend: d.workWeekWeekend,
      defaultWorkStart: d.defaultWorkStart,
      defaultWorkEnd: d.defaultWorkEnd,
      firstBranchName: d.firstBranchName.trim(),
    };
  }

  async submit(): Promise<boolean> {
    this.errorSignal.set(null);
    if (this.draftSignal().googleVerified && this.checkGoogleExpiry()) {
      this.errorSignal.set('onboarding.errors.googleExpired');
      return false;
    }
    this.submittingSignal.set(true);
    try {
      let key = this.draftSignal().idempotencyKey;
      if (!key) {
        key = crypto.randomUUID();
        this.patch({ idempotencyKey: key });
      }
      const result = await this.authApi.register(this.toRegisterBody(), key);
      this.clearGoogleTimer();
      this.patch({ result, googleRegistrationToken: null, stepIndex: ONBOARDING_STEP_IDS.length });
      sessionStorage.removeItem(ONBOARDING_DRAFT_KEY);
      return true;
    } catch (error) {
      const problem = problemDetailsFromError(error);
      const code = problem?.code;
      if (code === 'industry.invalid') {
        this.errorSignal.set('onboarding.errors.industryInvalid');
      } else if (code === GOOGLE_REGISTRATION_INVALID) {
        this.markGoogleExpired();
        this.errorSignal.set('onboarding.errors.googleExpired');
      } else if (code === 'auth.email_taken') {
        this.errorSignal.set('onboarding.errors.emailTaken');
      } else {
        this.errorSignal.set('onboarding.errors.registerFailed');
      }
      return false;
    } finally {
      this.submittingSignal.set(false);
    }
  }
}
