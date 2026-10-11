import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MeResponse, SetPasswordRequest, SignInMethodsResponse } from './me.models';

@Injectable({ providedIn: 'root' })
export class MeApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/me`;

  getMe(): Promise<MeResponse> {
    return firstValueFrom(this.http.get<MeResponse>(this.base, { withCredentials: true }));
  }

  getSignInMethods(): Promise<SignInMethodsResponse> {
    return firstValueFrom(
      this.http.get<SignInMethodsResponse>(`${this.base}/sign-in-methods`, {
        withCredentials: true,
      }),
    );
  }

  linkGoogle(credential: string): Promise<void> {
    return firstValueFrom(
      this.http.post(`${this.base}/google/link`, { credential }, {
        withCredentials: true,
        observe: 'response',
        responseType: 'text',
      }),
    ).then(() => undefined);
  }

  unlinkGoogle(): Promise<void> {
    return firstValueFrom(
      this.http.delete(`${this.base}/google`, {
        withCredentials: true,
        observe: 'response',
        responseType: 'text',
      }),
    ).then(() => undefined);
  }

  setPassword(body: SetPasswordRequest): Promise<void> {
    return firstValueFrom(
      this.http.post(`${this.base}/password`, body, {
        withCredentials: true,
        observe: 'response',
        responseType: 'text',
      }),
    ).then(() => undefined);
  }
}
