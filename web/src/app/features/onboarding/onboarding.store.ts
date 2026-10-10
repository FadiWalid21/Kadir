import { inject, Injectable, signal } from '@angular/core';
import { AuthApi, problemDetailsFromError } from '../../core/api/auth.api';
import { RegisterTenantRequest, RegisterTenantResponse } from '../../core/api/auth.models';
import { IndustryDto, TemplatesApi } from '../../core/api/templates.api';

export const ONBOARDING_DRAFT_KEY = 'kadr.onboarding.draft';

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

  readonly draft = this.draftSignal.asReadonly();
  readonly industries = this.industriesSignal.asReadonly();
  readonly submitting = this.submittingSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();

  hydrate(): void {
    try {
      const raw = sessionStorage.getItem(ONBOARDING_DRAFT_KEY);
      if (!raw) {
        return;
      }
      const parsed = JSON.parse(raw) as Partial<OnboardingDraft>;
      this.draftSignal.set({ ...DEFAULT_DRAFT, ...parsed, password: parsed.password ?? '' });
    } catch {
      sessionStorage.removeItem(ONBOARDING_DRAFT_KEY);
    }
  }

  persist(): void {
    const current = this.draftSignal();
    sessionStorage.setItem(ONBOARDING_DRAFT_KEY, JSON.stringify(current));
  }

  clear(): void {
    sessionStorage.removeItem(ONBOARDING_DRAFT_KEY);
    this.draftSignal.set({ ...DEFAULT_DRAFT });
    this.errorSignal.set(null);
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
        if (!d.email.includes('@')) {
          return 'onboarding.errors.email';
        }
        if (d.password.length < 10) {
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
    return {
      fullName: d.fullName.trim(),
      email: d.email.trim(),
      password: d.password,
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
    this.submittingSignal.set(true);
    try {
      let key = this.draftSignal().idempotencyKey;
      if (!key) {
        key = crypto.randomUUID();
        this.patch({ idempotencyKey: key });
      }
      const result = await this.authApi.register(this.toRegisterBody(), key);
      this.patch({ result, stepIndex: ONBOARDING_STEP_IDS.length });
      sessionStorage.removeItem(ONBOARDING_DRAFT_KEY);
      return true;
    } catch (error) {
      const problem = problemDetailsFromError(error);
      const code = problem?.code;
      if (code === 'industry.invalid') {
        this.errorSignal.set('onboarding.errors.industryInvalid');
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
