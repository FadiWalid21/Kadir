export interface LoginRequest {
  companySlug: string;
  email: string;
  password: string;
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
