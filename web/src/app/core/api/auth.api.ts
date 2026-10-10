import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthHttpConstants } from './auth.constants';
import {
  AccessTokenResponse,
  LoginRequest,
  ProblemDetails,
  RegisterTenantRequest,
  RegisterTenantResponse,
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
