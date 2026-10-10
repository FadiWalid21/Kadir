import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { ThemeService } from '../../core/theme/theme.service';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { DropdownMenuComponent } from '../../shared/ui/dropdown-menu/dropdown-menu.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'kadr-top-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, AvatarComponent, DropdownMenuComponent, IconComponent],
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
          <div dropdownContent class="flex flex-col">
            <button
              type="button"
              class="rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
              role="menuitem"
              (click)="onLogout()"
            >
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
