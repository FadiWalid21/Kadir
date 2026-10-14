import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationsStore } from '../../core/notifications/notifications.store';
import { InstallPromptService } from '../../core/pwa/install-prompt.service';
import { ThemeService } from '../../core/theme/theme.service';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { DropdownMenuComponent } from '../../shared/ui/dropdown-menu/dropdown-menu.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { TenantLogoComponent } from '../../shared/ui/tenant-logo/tenant-logo.component';

@Component({
  selector: 'kadr-top-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    RouterLink,
    AvatarComponent,
    DropdownMenuComponent,
    IconComponent,
    TenantLogoComponent,
  ],
  template: `
    <header
      class="kadr-safe-top flex h-14 items-center gap-3 border-b border-line bg-surface px-4 md:px-6"
      [attr.aria-label]="'shell.topBar' | transloco"
    >
      <div class="ms-auto flex items-center gap-1">
        <kadr-dropdown-menu [ariaLabel]="'shell.notifications' | transloco">
          <span
            dropdownTrigger
            class="relative inline-flex size-11 items-center justify-center"
            data-tour="top-notifications"
          >
            <kadr-icon name="bell" size="sm" />
            @if (unread() > 0) {
              <span
                class="absolute end-1.5 top-1.5 inline-flex min-w-4 items-center justify-center rounded-full bg-accent px-1 text-xs font-bold text-on-accent"
                aria-hidden="true"
              >
                {{ unread() > 9 ? '9+' : unread() }}
              </span>
            }
          </span>
          <div dropdownContent class="flex w-80 max-w-[calc(100vw-2rem)] flex-col">
            <div class="flex items-center justify-between gap-2 border-b border-line px-3 py-2">
              <span class="text-sm font-bold text-fg">{{ 'notifications.title' | transloco }}</span>
              <button
                type="button"
                class="text-xs text-accent underline-offset-2 hover:underline focus-visible:outline-none"
                (click)="markAll()"
              >
                {{ 'notifications.markAllRead' | transloco }}
              </button>
            </div>
            @if (previewState() === 'loading') {
              <p class="px-3 py-4 text-sm text-fg-2">{{ 'ui.loading' | transloco }}</p>
            } @else if (previewState() === 'error') {
              <p class="px-3 py-4 text-sm text-fg-2">{{ 'notifications.loadErrorBody' | transloco }}</p>
            } @else if (preview().length === 0) {
              <p class="px-3 py-4 text-sm text-fg-2">{{ 'notifications.emptyBody' | transloco }}</p>
            } @else {
              <ul class="max-h-80 overflow-y-auto py-1">
                @for (n of preview(); track n.id) {
                  <li>
                    <button
                      type="button"
                      class="flex w-full flex-col gap-0.5 px-3 py-2 text-start hover:bg-hover focus-visible:outline-none"
                      [class.bg-subtle]="!n.isRead"
                      role="menuitem"
                      (click)="openNotification(n.id, n.linkPath, n.isRead)"
                    >
                      <span class="text-sm font-medium text-fg">{{ titleOf(n.titleAr, n.titleEn) }}</span>
                      <span class="line-clamp-2 text-xs text-fg-2">{{ bodyOf(n.bodyAr, n.bodyEn) }}</span>
                    </button>
                  </li>
                }
              </ul>
            }
            <a
              routerLink="/notifications"
              class="border-t border-line px-3 py-2 text-center text-sm text-accent hover:underline focus-visible:outline-none"
              role="menuitem"
              (click)="closeHint.set(true)"
            >
              {{ 'notifications.viewAll' | transloco }}
            </a>
          </div>
        </kadr-dropdown-menu>

        <button
          type="button"
          class="inline-flex size-11 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
          [attr.aria-label]="'uiKit.language' | transloco"
          (click)="toggleLanguage()"
        >
          <kadr-icon name="globe" size="sm" />
        </button>

        <button
          type="button"
          class="inline-flex size-11 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
          [attr.aria-label]="'uiKit.theme' | transloco"
          (click)="toggleTheme()"
        >
          <kadr-icon [name]="theme.resolvedTheme() === 'dark' ? 'sun' : 'moon'" size="sm" />
        </button>

        <kadr-dropdown-menu [ariaLabel]="'shell.userMenu' | transloco">
          <span dropdownTrigger class="inline-flex items-center gap-2 px-1">
            <kadr-avatar
              [name]="userName()"
              [imageUrl]="photoUrl()"
              size="sm"
              [ariaLabel]="userName()"
            />
            <span class="hidden min-w-0 text-start md:block">
              <span
                class="block max-w-40 truncate text-sm font-medium text-fg"
                [attr.title]="userName()"
              >
                {{ userName() }}
              </span>
            </span>
          </span>
          <div dropdownContent class="flex min-w-60 max-w-72 flex-col">
            <div
              class="flex items-center gap-3 border-b border-line px-3 py-3 text-start"
              data-testid="user-card"
            >
              <kadr-avatar
                [name]="userName()"
                [imageUrl]="photoUrl()"
                size="md"
                [ariaLabel]="userName()"
              />
              <div class="flex min-w-0 flex-1 flex-col gap-1">
                <span class="truncate text-sm font-bold text-fg" [attr.title]="userName()">
                  {{ userName() }}
                </span>
                <span class="truncate text-xs text-fg-2" [attr.title]="userEmail()">
                  <bdi>{{ userEmail() }}</bdi>
                </span>
                @if (userRole()) {
                  <span
                    class="inline-flex h-6 w-fit max-w-full items-center truncate rounded-full bg-subtle px-2.5 text-xs font-medium text-fg-2"
                    [attr.title]="userRole()"
                  >
                    {{ userRole() }}
                  </span>
                }
              </div>
            </div>

            <div
              class="flex items-center gap-3 border-b border-line px-3 py-3 text-start"
              data-testid="tenant-card"
            >
              <kadr-tenant-logo
                [logoUrl]="tenantLogoUrl()"
                [fallbackName]="tenantName()"
                size="sm"
              />
              <span
                class="min-w-0 flex-1 truncate text-sm font-medium text-fg"
                [attr.title]="tenantName()"
              >
                {{ tenantName() }}
              </span>
            </div>

            <a
              routerLink="/account"
              class="mt-1 flex items-center gap-3 rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
              role="menuitem"
              data-testid="menu-account"
            >
              <kadr-icon name="user" size="sm" />
              {{ 'shell.account' | transloco }}
            </a>
            <a
              routerLink="/account/notifications"
              class="flex items-center gap-3 rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
              role="menuitem"
            >
              <kadr-icon name="bell" size="sm" />
              {{ 'notifications.prefsTitle' | transloco }}
            </a>

            @if (install.showMenuItem()) {
              <button
                type="button"
                class="flex items-center gap-3 rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
                role="menuitem"
                data-testid="menu-install"
                (click)="onInstall()"
              >
                <kadr-icon name="download" size="sm" />
                {{ 'pwa.install.menu' | transloco }}
              </button>
            }

            <button
              type="button"
              class="flex items-center gap-3 rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
              role="menuitem"
              (click)="toggleLanguage()"
            >
              <kadr-icon name="globe" size="sm" />
              {{ 'shell.switchLanguage' | transloco }}
            </button>
            <button
              type="button"
              class="flex items-center gap-3 rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
              role="menuitem"
              (click)="onLogout()"
            >
              <kadr-icon name="logout" size="sm" [rtlFlip]="true" />
              {{ 'auth.signOut' | transloco }}
            </button>
          </div>
        </kadr-dropdown-menu>
      </div>
    </header>
  `,
})
export class TopBarComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationsStore);
  protected readonly theme = inject(ThemeService);
  protected readonly install = inject(InstallPromptService);

  protected readonly userName = computed(() => this.auth.me()?.fullName ?? '');
  protected readonly userEmail = computed(() => this.auth.me()?.email ?? '');
  protected readonly userRole = computed(() => this.auth.me()?.roles[0] ?? '');
  protected readonly photoUrl = computed(() => this.auth.me()?.photoUrl ?? null);
  protected readonly tenantName = computed(() => this.auth.me()?.tenant.name ?? '');
  protected readonly tenantLogoUrl = computed(() => this.auth.me()?.tenant.logoUrl ?? null);
  protected readonly unread = this.notifications.unreadCount;
  protected readonly preview = this.notifications.preview;
  protected readonly previewState = this.notifications.previewState;
  protected readonly closeHint = signal(false);

  ngOnInit(): void {
    void this.notifications.refreshUnread();
    void this.notifications.loadPreview();
  }

  titleOf(ar: string, en: string): string {
    return this.transloco.getActiveLang() === 'en' ? en : ar;
  }

  bodyOf(ar: string, en: string): string {
    return this.transloco.getActiveLang() === 'en' ? en : ar;
  }

  async openNotification(id: string, linkPath: string | null, isRead: boolean): Promise<void> {
    if (!isRead) {
      try {
        await this.notifications.markRead(id);
      } catch {
        // Navigate anyway.
      }
    }
    if (linkPath) {
      void this.router.navigateByUrl(linkPath);
    } else {
      void this.router.navigateByUrl('/notifications');
    }
  }

  async markAll(): Promise<void> {
    try {
      await this.notifications.markAllRead();
    } catch {
      // Ignore; user can retry from the page.
    }
  }

  toggleLanguage(): void {
    const next = this.theme.language() === 'ar' ? 'en' : 'ar';
    this.theme.setLanguage(next);
    this.transloco.setActiveLang(next);
  }

  toggleTheme(): void {
    this.theme.setTheme(this.theme.resolvedTheme() === 'dark' ? 'light' : 'dark');
  }

  onLogout(): void {
    void this.auth.logout();
  }

  onInstall(): void {
    void this.install.promptInstall();
  }
}
