import { inject, Injectable, signal } from '@angular/core';
import { NotificationsApi, NotificationDto } from '../api/notifications.api';

@Injectable({ providedIn: 'root' })
export class NotificationsStore {
  private readonly api = inject(NotificationsApi);

  readonly unreadCount = signal(0);
  readonly preview = signal<NotificationDto[]>([]);
  readonly previewState = signal<'idle' | 'loading' | 'ready' | 'error'>('idle');

  async refreshUnread(): Promise<void> {
    try {
      const result = await this.api.unreadCount();
      this.unreadCount.set(result.count);
    } catch {
      // Keep last known count; bell stays usable.
    }
  }

  async loadPreview(): Promise<void> {
    this.previewState.set('loading');
    try {
      const page = await this.api.list(1, 8, false);
      this.preview.set(page.items);
      this.unreadCount.set(page.unreadCount);
      this.previewState.set('ready');
    } catch {
      this.previewState.set('error');
    }
  }

  async markRead(id: string): Promise<void> {
    await this.api.markRead(id);
    this.preview.update((items) =>
      items.map((n) => (n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n)),
    );
    await this.refreshUnread();
  }

  async markAllRead(): Promise<void> {
    await this.api.markAllRead();
    this.preview.update((items) =>
      items.map((n) => ({ ...n, isRead: true, readAt: n.readAt ?? new Date().toISOString() })),
    );
    this.unreadCount.set(0);
  }
}
