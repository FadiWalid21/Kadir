import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  TenantLogoResponse,
  TenantSettingsResponse,
  UpdateTenantSettingsRequest,
} from './tenant.models';

@Injectable({ providedIn: 'root' })
export class TenantApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/tenant`;

  getSettings(): Promise<TenantSettingsResponse> {
    return firstValueFrom(
      this.http.get<TenantSettingsResponse>(`${this.base}/settings`, { withCredentials: true }),
    );
  }

  updateSettings(body: UpdateTenantSettingsRequest): Promise<TenantSettingsResponse> {
    return firstValueFrom(
      this.http.put<TenantSettingsResponse>(`${this.base}/settings`, body, {
        withCredentials: true,
      }),
    );
  }

  uploadLogo(file: File): Promise<TenantLogoResponse> {
    const form = new FormData();
    form.append('file', file, file.name);
    return firstValueFrom(
      this.http.put<TenantLogoResponse>(`${this.base}/logo`, form, { withCredentials: true }),
    );
  }

  deleteLogo(): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.base}/logo`, { withCredentials: true }),
    );
  }
}
