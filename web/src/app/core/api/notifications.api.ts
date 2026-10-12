import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface NotificationDto {
  id: string;
  type: string;
  titleAr: string;
  titleEn: string;
  bodyAr: string;
  bodyEn: string;
  linkPath: string | null;
  metadataJson: string | null;
  createdAt: string;
  readAt: string | null;
  isRead: boolean;
}

export interface PagedNotificationsDto {
  items: NotificationDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  unreadCount: number;
}

export interface NotificationPreferenceDto {
  eventType: string;
  inAppEnabled: boolean;
  emailEnabled: boolean;
  pushEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/notifications`;

  list(page = 1, pageSize = 20, unreadOnly = false): Promise<PagedNotificationsDto> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (unreadOnly) {
      params = params.set('unreadOnly', 'true');
    }
    return firstValueFrom(
      this.http.get<PagedNotificationsDto>(this.base, { params, withCredentials: true }),
    );
  }

  unreadCount(): Promise<{ count: number }> {
    return firstValueFrom(
      this.http.get<{ count: number }>(`${this.base}/unread-count`, { withCredentials: true }),
    );
  }

  markRead(id: string): Promise<NotificationDto> {
    return firstValueFrom(
      this.http.post<NotificationDto>(`${this.base}/${id}/read`, {}, { withCredentials: true }),
    );
  }

  markAllRead(): Promise<{ updated: number }> {
    return firstValueFrom(
      this.http.post<{ updated: number }>(`${this.base}/read-all`, {}, { withCredentials: true }),
    );
  }

  getPreferences(): Promise<{ items: NotificationPreferenceDto[] }> {
    return firstValueFrom(
      this.http.get<{ items: NotificationPreferenceDto[] }>(`${this.base}/preferences`, {
        withCredentials: true,
      }),
    );
  }

  updatePreferences(items: NotificationPreferenceDto[]): Promise<{ items: NotificationPreferenceDto[] }> {
    return firstValueFrom(
      this.http.put<{ items: NotificationPreferenceDto[] }>(
        `${this.base}/preferences`,
        { items },
        { withCredentials: true },
      ),
    );
  }
}
