import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { NotificationDto, NotificationsApi } from '../../core/api/notifications.api';
import { NotificationsStore } from '../../core/notifications/notifications.store';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';

type State = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-notifications-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    RouterLink,
    PageHeaderComponent,
    ButtonComponent,
    EmptyStateComponent,
    SkeletonComponent,
    NetworkErrorComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-3xl flex-col gap-4">
      <kadr-page-header
        [title]="'notifications.title' | transloco"
        [subtitle]="'notifications.subtitle' | transloco"
      >
        <a routerLink="/account/notifications" class="mt-2 text-sm text-accent underline-offset-2 hover:underline">
          {{ 'notifications.preferencesLink' | transloco }}
        </a>
      </kadr-page-header>

      @if (state() === 'loading') {
        <div class="flex flex-col gap-3" role="status" [attr.aria-label]="'ui.loading' | transloco">
          @for (_ of skeletons; track $index) {
            <kadr-skeleton height="4.5rem" />
          }
        </div>
      } @else if (state() === 'error') {
        <kadr-network-error
          [title]="'notifications.loadErrorTitle' | transloco"
          [description]="'notifications.loadErrorBody' | transloco"
          (retry)="load()"
        />
      } @else if (items().length === 0) {
        <kadr-empty-state
          [title]="'notifications.emptyTitle' | transloco"
          [description]="'notifications.emptyBody' | transloco"
        />
      } @else {
        <div class="flex justify-end">
          <kadr-button variant="secondary" type="button" (click)="markAll()">
            {{ 'notifications.markAllRead' | transloco }}
          </kadr-button>
        </div>
        <ul class="flex flex-col gap-2">
          @for (n of items(); track n.id) {
            <li>
              <button
                type="button"
                class="flex w-full flex-col gap-1 rounded-md border border-line bg-surface px-4 py-3 text-start hover:bg-hover focus-visible:outline-none"
                [class.bg-subtle]="!n.isRead"
                (click)="open(n)"
              >
                <div class="flex items-start justify-between gap-3">
                  <span class="text-sm font-bold text-fg">{{ titleOf(n) }}</span>
                  @if (!n.isRead) {
                    <span class="mt-1 inline-flex size-2 shrink-0 rounded-full bg-accent" aria-hidden="true"></span>
                  }
                </div>
                <p class="text-sm text-fg-2">{{ bodyOf(n) }}</p>
                <span class="text-xs text-fg-muted tabular-nums">{{ formatDate(n.createdAt) }}</span>
              </button>
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class NotificationsPage implements OnInit {
  private readonly api = inject(NotificationsApi);
  private readonly store = inject(NotificationsStore);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);

  protected readonly state = signal<State>('loading');
  protected readonly items = signal<NotificationDto[]>([]);
  protected readonly skeletons = Array.from({ length: 6 });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.state.set('loading');
    try {
      const page = await this.api.list(1, 50);
      this.items.set(page.items);
      this.store.unreadCount.set(page.unreadCount);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  titleOf(n: NotificationDto): string {
    return this.transloco.getActiveLang() === 'en' ? n.titleEn : n.titleAr;
  }

  bodyOf(n: NotificationDto): string {
    return this.transloco.getActiveLang() === 'en' ? n.bodyEn : n.bodyAr;
  }

  formatDate(value: string): string {
    try {
      return new Intl.DateTimeFormat(this.transloco.getActiveLang() === 'en' ? 'en' : 'ar', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value));
    } catch {
      return value;
    }
  }

  async open(n: NotificationDto): Promise<void> {
    if (!n.isRead) {
      try {
        await this.store.markRead(n.id);
        this.items.update((list) =>
          list.map((x) => (x.id === n.id ? { ...x, isRead: true, readAt: new Date().toISOString() } : x)),
        );
      } catch {
        // Still navigate.
      }
    }
    if (n.linkPath) {
      void this.router.navigateByUrl(n.linkPath);
    }
  }

  async markAll(): Promise<void> {
    await this.store.markAllRead();
    this.items.update((list) =>
      list.map((n) => ({ ...n, isRead: true, readAt: n.readAt ?? new Date().toISOString() })),
    );
  }
}
