export interface LoginRequest {
  companySlug: string;
  email: string;
  password: string;
}

export interface RegisterTenantRequest {
  fullName: string;
  email: string;
  password: string;
  companyName: string;
  industryCode: string;
  timeZone?: string | null;
  defaultLanguage?: string | null;
  countryCode?: string | null;
  workWeekDays?: string[] | null;
  workWeekWeekend?: string[] | null;
  defaultWorkStart?: string | null;
  defaultWorkEnd?: string | null;
  firstBranchName?: string | null;
}

export interface RegisterTenantResponse {
  tenantId: string;
  slug: string;
  trialEndsAt: string;
}

export interface AccessTokenResponse {
  accessToken: string;
  expiresIn: number;
  tokenType: string;
}

export interface VerifyEmailRequest {
  token: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}
