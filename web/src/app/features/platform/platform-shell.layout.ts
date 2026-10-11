import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PlatformAuthService } from '../../core/platform/platform-auth.service';
import { ThemeService } from '../../core/theme/theme.service';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { DropdownMenuComponent } from '../../shared/ui/dropdown-menu/dropdown-menu.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { ToastHostComponent } from '../../shared/ui/toast/toast.component';

@Component({
  selector: 'kadr-platform-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TranslocoPipe,
    ToastHostComponent,
    IconComponent,
    AvatarComponent,
    DropdownMenuComponent,
  ],
  template: `
    <div class="flex min-h-dvh flex-col bg-app">
      <div class="flex h-8 items-center justify-center bg-ink px-4 text-center text-xs text-on-ink">
        <p class="truncate">{{ 'platform.strip' | transloco }}</p>
      </div>

      <header class="border-b border-line bg-surface">
        <div
          class="mx-auto flex w-full max-w-screen-xl flex-wrap items-center gap-3 px-4 py-2.5 md:px-6"
        >
          <a
            routerLink="/platform/companies"
            class="inline-flex items-center gap-2 focus-visible:outline-none"
          >
            <picture>
              <source type="image/webp" srcset="/brand/logo-kadr-128.webp" />
              <img
                src="/brand/logo-kadr-128.png"
                alt=""
                width="32"
                height="32"
                class="size-8 shrink-0 object-contain"
              />
            </picture>
            <span class="text-base font-bold text-fg">{{ 'platform.brand' | transloco }}</span>
            <span
              class="rounded-md bg-accent-soft px-2 py-0.5 text-xs font-medium text-accent-soft-fg"
            >
              {{ 'platform.tag' | transloco }}
            </span>
          </a>

          <nav
            class="flex w-full flex-wrap items-center gap-1 md:ms-4 md:w-auto"
            [attr.aria-label]="'platform.navAria' | transloco"
          >
            @for (item of nav; track item.path) {
              <a
                [routerLink]="item.path"
                routerLinkActive
                #rla="routerLinkActive"
                [routerLinkActiveOptions]="item.exact ? { exact: true } : { exact: false }"
                class="inline-flex h-9 items-center rounded-md px-3 text-sm focus-visible:outline-none"
                [class.bg-accent]="rla.isActive"
                [class.text-on-accent]="rla.isActive"
                [class.text-fg-2]="!rla.isActive"
                [class.hover:bg-hover]="!rla.isActive"
                [class.hover:text-fg]="!rla.isActive"
              >
                {{ item.key | transloco }}
              </a>
            }
          </nav>

          <div class="ms-auto">
            <kadr-dropdown-menu [ariaLabel]="'platform.operator.menuAria' | transloco">
              <span dropdownTrigger class="inline-flex items-center gap-2 px-1">
                <kadr-avatar
                  [name]="operatorLabel()"
                  size="sm"
                  [ariaLabel]="'platform.operator.role' | transloco"
                />
                <span class="hidden min-w-0 text-start sm:block">
                  <span class="block max-w-44 truncate text-sm font-medium text-fg">
                    {{ operatorLabel() }}
                  </span>
                  <span class="block text-xs text-fg-muted">
                    {{ 'platform.operator.role' | transloco }}
                  </span>
                </span>
                <kadr-icon name="chevron-down" size="sm" class="text-fg-muted" />
              </span>
              <div dropdownContent class="flex min-w-56 flex-col">
                @if (auth.email(); as email) {
                  <div class="border-b border-line px-3 py-2 text-start">
                    <p class="truncate text-xs text-fg-2"><bdi>{{ email }}</bdi></p>
                  </div>
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
                  (click)="toggleTheme()"
                >
                  <kadr-icon [name]="theme.resolvedTheme() === 'dark' ? 'sun' : 'moon'" size="sm" />
                  {{ 'uiKit.theme' | transloco }}
                </button>
                <button
                  type="button"
                  class="flex items-center gap-3 rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
                  role="menuitem"
                  (click)="logout()"
                >
                  <kadr-icon name="logout" size="sm" [rtlFlip]="true" />
                  {{ 'platform.signOut' | transloco }}
                </button>
              </div>
            </kadr-dropdown-menu>
          </div>
        </div>
      </header>

      <main class="mx-auto w-full max-w-screen-xl flex-1 px-4 py-6 md:px-6">
        <router-outlet />
      </main>
      <kadr-toast-host />
    </div>
  `,
})
export class PlatformShellLayout {
  protected readonly auth = inject(PlatformAuthService);
  private readonly transloco = inject(TranslocoService);
  protected readonly theme = inject(ThemeService);

  protected readonly nav = [
    { path: '/platform/companies', key: 'platform.nav.companies', exact: false },
    { path: '/platform/plans', key: 'platform.nav.plans', exact: true },
    { path: '/platform/audit', key: 'platform.nav.audit', exact: true },
    { path: '/platform/health', key: 'platform.nav.health', exact: true },
  ] as const;

  protected readonly operatorLabel = computed(
    () => this.auth.email() ?? this.transloco.translate('platform.operator.role'),
  );

  toggleLanguage(): void {
    const next = this.theme.language() === 'ar' ? 'en' : 'ar';
    this.theme.setLanguage(next);
    this.transloco.setActiveLang(next);
  }

  toggleTheme(): void {
    this.theme.setTheme(this.theme.resolvedTheme() === 'dark' ? 'light' : 'dark');
  }

  logout(): void {
    void this.auth.logout();
  }
}
