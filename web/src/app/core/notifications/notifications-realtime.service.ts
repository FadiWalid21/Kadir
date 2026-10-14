import { isPlatformBrowser } from '@angular/common';
import { Injectable, NgZone, PLATFORM_ID, inject } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { NotificationsStore } from './notifications.store';

@Injectable({ providedIn: 'root' })
export class NotificationsRealtimeService {
  private readonly auth = inject(AuthService);
  private readonly store = inject(NotificationsStore);
  private readonly zone = inject(NgZone);
  private readonly platformId = inject(PLATFORM_ID);
  private connection: HubConnection | null = null;
  private started = false;

  /** Call once from the authenticated shell. */
  start(): void {
    if (!isPlatformBrowser(this.platformId) || this.started) {
      return;
    }
    if (this.auth.status() !== 'authenticated') {
      return;
    }
    this.started = true;
    void this.connect();
  }

  async stop(): Promise<void> {
    this.started = false;
    const conn = this.connection;
    this.connection = null;
    if (conn) {
      try {
        await conn.stop();
      } catch {
        /* ignore */
      }
    }
  }

  private async connect(): Promise<void> {
    const hubUrl = resolveHubUrl(environment.apiBaseUrl);
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.auth.accessToken() ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('notificationCreated', () => {
      this.zone.run(() => {
        void this.store.refreshUnread();
        void this.store.loadPreview();
      });
    });

    this.connection = connection;
    try {
      await connection.start();
    } catch {
      // Bell stays on pull; reconnect will retry.
    }
  }
}

export function resolveHubUrl(apiBaseUrl: string): string {
  const trimmed = apiBaseUrl.replace(/\/$/, '');
  if (/^https?:\/\//i.test(trimmed)) {
    const withoutApi = trimmed.replace(/\/api\/v1$/i, '');
    return `${withoutApi}/hubs/notifications`;
  }
  return '/hubs/notifications';
}

export function isConnected(connection: HubConnection | null): boolean {
  return connection?.state === HubConnectionState.Connected;
}
