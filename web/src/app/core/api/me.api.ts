import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MeResponse } from './me.models';

@Injectable({ providedIn: 'root' })
export class MeApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/me`;

  getMe(): Promise<MeResponse> {
    return firstValueFrom(this.http.get<MeResponse>(this.base, { withCredentials: true }));
  }
}
