import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ValidateInvitationResponse {
  status: 'valid' | 'expired' | 'used' | 'revoked' | 'invalid';
  email: string | null;
  companyName: string | null;
  roleName: string | null;
}

@Injectable({ providedIn: 'root' })
export class InvitationsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/auth`;

  validate(token: string): Promise<ValidateInvitationResponse> {
    return firstValueFrom(
      this.http.post<ValidateInvitationResponse>(`${this.base}/invitations/validate`, { token }),
    );
  }

  accept(token: string, fullName: string, password: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.base}/accept-invitation`, { token, fullName, password }),
    );
  }
}
