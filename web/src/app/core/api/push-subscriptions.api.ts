import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PushSubscriptionDeviceDto {
  id: string;
  deviceLabel: string | null;
  createdAt: string;
  lastSeenAt: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class PushSubscriptionsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/me/push-subscriptions`;

  vapidPublicKey(): Promise<{ publicKey: string }> {
    return firstValueFrom(
      this.http.get<{ publicKey: string }>(`${this.base}/vapid-public-key`, {
        withCredentials: true,
      }),
    );
  }

  list(): Promise<{ items: PushSubscriptionDeviceDto[] }> {
    return firstValueFrom(
      this.http.get<{ items: PushSubscriptionDeviceDto[] }>(this.base, { withCredentials: true }),
    );
  }

  upsert(endpoint: string, p256dh: string, auth: string, deviceLabel?: string): Promise<PushSubscriptionDeviceDto> {
    return firstValueFrom(
      this.http.post<PushSubscriptionDeviceDto>(
        this.base,
        { endpoint, keys: { p256dh, auth }, deviceLabel: deviceLabel ?? null },
        { withCredentials: true },
      ),
    );
  }

  delete(id: string): Promise<void> {
    return firstValueFrom(
      this.http.delete(`${this.base}/${id}`, {
        withCredentials: true,
        observe: 'response',
        responseType: 'text',
      }),
    ).then(() => undefined);
  }

  sendTest(): Promise<void> {
    return firstValueFrom(
      this.http.post(`${this.base}/test`, {}, {
        withCredentials: true,
        observe: 'response',
        responseType: 'text',
      }),
    ).then(() => undefined);
  }
}
