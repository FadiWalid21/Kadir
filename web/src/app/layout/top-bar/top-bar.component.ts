import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { ThemeService } from '../../core/theme/theme.service';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { DropdownMenuComponent } from '../../shared/ui/dropdown-menu/dropdown-menu.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'kadr-top-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, AvatarComponent, DropdownMenuComponent, IconComponent],
  template: `
    <header
      class="flex h-14 items-center gap-3 border-b border-line bg-surface px-4 md:px-6"
      [attr.aria-label]="'shell.topBar' | transloco"
    >
      <div
        class="hidden min-w-0 flex-1 items-center gap-2 rounded-md border border-line bg-subtle px-3 text-sm text-fg-muted sm:flex"
        [attr.title]="'shell.notAvailableYet' | transloco"
        role="search"
      >
        <kadr-icon name="search" size="sm" />
        <span class="truncate py-2">{{ 'shell.searchPlaceholder' | transloco }}</span>
      </div>

      <div class="ms-auto flex items-center gap-1">
        <button
          type="button"
          class="inline-flex size-11 items-center justify-center rounded-md text-fg-muted opacity-50 focus-visible:outline-none"
          disabled
          [attr.aria-label]="'shell.notificationsDisabled' | transloco"
          [attr.title]="'shell.notAvailableYet' | transloco"
        >
          <kadr-icon name="bell" size="sm" />
        </button>

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
            <kadr-avatar [name]="userName()" size="sm" [ariaLabel]="userName()" />
            <span class="hidden min-w-0 text-start md:block">
              <span class="block max-w-40 truncate text-sm font-medium text-fg">{{ userName() }}</span>
            </span>
          </span>
          <div dropdownContent class="flex min-w-60 max-w-72 flex-col">
            <div
              class="flex items-center gap-3 border-b border-line px-3 py-3 text-start"
              data-testid="user-card"
            >
              <kadr-avatar [name]="userName()" size="md" />
              <div class="flex min-w-0 flex-1 flex-col gap-1">
                <span class="truncate text-sm font-bold text-fg">{{ userName() }}</span>
                <span class="truncate text-xs text-fg-2"><bdi>{{ userEmail() }}</bdi></span>
                @if (userRole()) {
                  <span
                    class="inline-flex h-6 w-fit max-w-full items-center truncate rounded-full bg-subtle px-2.5 text-xs font-medium text-fg-2"
                  >
                    {{ userRole() }}
                  </span>
                }
              </div>
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
              class="flex items-center gap-3 rounded-sm px-3 py-2 text-start text-sm text-danger hover:bg-hover focus-visible:outline-none"
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
export class TopBarComponent {
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);
  protected readonly theme = inject(ThemeService);

  protected readonly userName = computed(() => this.auth.me()?.fullName ?? '');
  protected readonly userEmail = computed(() => this.auth.me()?.email ?? '');
  protected readonly userRole = computed(() => this.auth.me()?.roles[0] ?? '');

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
}
