import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthHttpConstants } from './auth.constants';
import {
  AccessTokenResponse,
  AuthProvidersResponse,
  ForgotPasswordRequest,
  ForgotPasswordResponse,
  GoogleLoginResponse,
  GoogleNonceResponse,
  GoogleSelectTenantRequest,
  LoginRequest,
  ProblemDetails,
  RegisterTenantRequest,
  RegisterTenantResponse,
  ResetPasswordRequest,
  VerifyEmailRequest,
} from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/auth`;

  register(body: RegisterTenantRequest, idempotencyKey: string): Promise<RegisterTenantResponse> {
    return firstValueFrom(
      this.http.post<RegisterTenantResponse>(`${this.base}/register`, body, {
        headers: { 'Idempotency-Key': idempotencyKey },
      }),
    );
  }

  login(body: LoginRequest): Promise<AccessTokenResponse> {
    return firstValueFrom(
      this.http.post<AccessTokenResponse>(`${this.base}/login`, body, {
        withCredentials: true,
      }),
    );
  }

  getProviders(): Promise<AuthProvidersResponse> {
    return firstValueFrom(this.http.get<AuthProvidersResponse>(`${this.base}/providers`));
  }

  issueGoogleNonce(): Promise<GoogleNonceResponse> {
    return firstValueFrom(this.http.post<GoogleNonceResponse>(`${this.base}/google/nonce`, {}));
  }

  googleLogin(credential: string): Promise<GoogleLoginResponse> {
    return firstValueFrom(
      this.http.post<GoogleLoginResponse>(
        `${this.base}/google/login`,
        { credential },
        { withCredentials: true },
      ),
    );
  }

  googleSelectTenant(body: GoogleSelectTenantRequest): Promise<AccessTokenResponse> {
    return firstValueFrom(
      this.http.post<AccessTokenResponse>(`${this.base}/google/select-tenant`, body, {
        withCredentials: true,
      }),
    );
  }

  forgotPassword(body: ForgotPasswordRequest): Promise<ForgotPasswordResponse> {
    return firstValueFrom(
      this.http.post<ForgotPasswordResponse>(`${this.base}/forgot-password`, body),
    );
  }

  resetPassword(body: ResetPasswordRequest): Promise<void> {
    return firstValueFrom(
      this.http.post(`${this.base}/reset-password`, body, {
        observe: 'response',
        responseType: 'text',
      }),
    ).then(() => undefined);
  }

  refresh(): Promise<AccessTokenResponse> {
    return firstValueFrom(
      this.http.post<AccessTokenResponse>(
        `${this.base}/refresh`,
        {},
        {
          withCredentials: true,
          headers: {
            [AuthHttpConstants.csrfHeaderName]: AuthHttpConstants.csrfHeaderValue,
          },
        },
      ),
    );
  }

  logout(): Promise<void> {
    return firstValueFrom(
      this.http.post(
        `${this.base}/logout`,
        {},
        {
          withCredentials: true,
          headers: {
            [AuthHttpConstants.csrfHeaderName]: AuthHttpConstants.csrfHeaderValue,
          },
          observe: 'response',
          responseType: 'text',
        },
      ),
    ).then(() => undefined);
  }

  verifyEmail(body: VerifyEmailRequest): Promise<void> {
    return firstValueFrom(
      this.http.post(
        `${this.base}/verify-email`,
        body,
        { observe: 'response', responseType: 'text' },
      ),
    ).then(() => undefined);
  }
}

export function problemDetailsFromError(error: unknown): ProblemDetails | null {
  if (!(error instanceof HttpErrorResponse)) {
    return null;
  }
  const body = error.error;
  if (body && typeof body === 'object' && 'code' in body) {
    return body as ProblemDetails;
  }
  return { status: error.status, title: error.statusText };
}
