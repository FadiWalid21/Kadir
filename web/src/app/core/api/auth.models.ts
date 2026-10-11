export interface LoginRequest {
  companySlug: string;
  email: string;
  password: string;
}

export interface RegisterTenantRequest {
  fullName: string;
  /** Omitted when `googleRegistrationToken` is sent: the identity email comes from the token. */
  email?: string;
  /** Optional with a Google registration token. */
  password?: string;
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
  googleRegistrationToken?: string | null;
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
  /** Present on `auth.google_account_not_found` (404). One-time, 30 minutes. */
  registrationToken?: string;
}

export interface GoogleProviderDto {
  enabled: boolean;
  clientId?: string | null;
}

export interface AuthProvidersResponse {
  google: GoogleProviderDto;
}

export interface GoogleNonceResponse {
  nonce: string;
  expiresAt: string;
}

export interface GoogleLoginRequest {
  credential: string;
}

export interface GoogleCompanyOption {
  tenantId: string;
  name: string;
  roleName: string;
  lastLoginAt?: string | null;
}

export interface GoogleTenantSelectionResponse {
  status: 'tenant_selection_required';
  selectionToken: string;
  companies: GoogleCompanyOption[];
}

export type GoogleLoginResponse = AccessTokenResponse | GoogleTenantSelectionResponse;

export interface GoogleSelectTenantRequest {
  selectionToken: string;
  tenantId: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ForgotPasswordResponse {
  status: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export function isTenantSelection(
  response: GoogleLoginResponse,
): response is GoogleTenantSelectionResponse {
  return 'status' in response && response.status === 'tenant_selection_required';
}
