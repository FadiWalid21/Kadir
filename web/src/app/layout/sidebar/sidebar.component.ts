import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { IconComponent, IconName } from '../../shared/ui/icon/icon.component';
import { TenantLogoComponent } from '../../shared/ui/tenant-logo/tenant-logo.component';

export interface NavItem {
  labelKey: string;
  icon: IconName;
  route?: string;
  disabled?: boolean;
  permission?: string;
}

@Component({
  selector: 'kadr-sidebar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, RouterLinkActive, IconComponent, TenantLogoComponent],
  template: `
    <aside
      class="flex h-dvh flex-col border-e border-line bg-surface transition-[width] duration-200"
      [class.w-60]="!collapsed()"
      [class.w-16]="collapsed()"
      [attr.aria-label]="'shell.sidebar' | transloco"
    >
      <div class="flex items-center gap-3 border-b border-line px-3 py-4">
        <kadr-tenant-logo
          class="shrink-0"
          [logoUrl]="tenantLogoUrl()"
          [logoVersion]="tenantLogoVersion()"
          [fallbackName]="tenantName()"
          size="sm"
        />
        @if (!collapsed()) {
          <div class="min-w-0 flex-1 text-start">
            <p class="truncate text-sm font-bold text-fg">{{ tenantName() || ('app.name' | transloco) }}</p>
            <p class="truncate text-xs text-fg-muted">{{ 'app.name' | transloco }}</p>
          </div>
        }
        <button
          type="button"
          class="inline-flex size-11 shrink-0 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
          [attr.aria-label]="(collapsed() ? 'shell.expandSidebar' : 'shell.collapseSidebar') | transloco"
          (click)="collapsedChange.emit(!collapsed())"
        >
          <kadr-icon name="chevrons" [rtlFlip]="true" size="sm" />
        </button>
      </div>

      <nav class="flex flex-1 flex-col gap-1 overflow-y-auto p-2">
        @for (item of visibleItems(); track item.labelKey) {
          @if (item.disabled || !item.route) {
            <span
              class="flex min-h-11 items-center gap-3 rounded-md px-3 py-2 text-sm text-fg-muted opacity-60"
              [attr.title]="'shell.notAvailableYet' | transloco"
            >
              <kadr-icon [name]="item.icon" size="sm" />
              @if (!collapsed()) {
                <span class="truncate">{{ item.labelKey | transloco }}</span>
              }
            </span>
          } @else {
            <a
              [routerLink]="item.route"
              routerLinkActive="bg-accent text-on-accent"
              [routerLinkActiveOptions]="{ exact: item.route === '/' }"
              class="flex min-h-11 items-center gap-3 rounded-md px-3 py-2 text-sm text-fg hover:bg-hover focus-visible:outline-none"
              [attr.title]="collapsed() ? (item.labelKey | transloco) : null"
            >
              <kadr-icon [name]="item.icon" size="sm" />
              @if (!collapsed()) {
                <span class="truncate">{{ item.labelKey | transloco }}</span>
              }
            </a>
          }
        }
      </nav>
    </aside>
  `,
})
export class SidebarComponent {
  private readonly auth = inject(AuthService);

  readonly collapsed = input(false);
  readonly collapsedChange = output<boolean>();

  protected readonly tenantName = computed(
    () => this.auth.me()?.tenant.name ?? '',
  );

  private readonly items: NavItem[] = [
    { labelKey: 'shell.nav.home', icon: 'home', route: '/' },
    {
      labelKey: 'shell.nav.employees',
      icon: 'users',
      route: '/employees',
      permission: PermissionCodes.EmployeesRead,
    },
    { labelKey: 'shell.nav.org', icon: 'building', route: '/org', permission: PermissionCodes.OrgRead },
    { labelKey: 'shell.nav.attendance', icon: 'clock', disabled: true },
    { labelKey: 'shell.nav.leave', icon: 'calendar', disabled: true },
    { labelKey: 'shell.nav.requests', icon: 'inbox', disabled: true },
    { labelKey: 'shell.nav.reports', icon: 'chart', disabled: true },
    {
      labelKey: 'shell.nav.settings',
      icon: 'settings',
      route: '/settings/company',
      permission: PermissionCodes.TenantSettingsRead,
    },
  ];

  protected readonly tenantLogoUrl = computed(() => this.auth.me()?.tenant.logoUrl ?? null);

  /** Forces reload when /me logoUrl changes (including replace with a new file id). */
  protected readonly tenantLogoVersion = computed(() => this.tenantLogoUrl() ?? '');

  protected readonly visibleItems = computed(() =>
    this.items.filter((item) => {
      if (!item.permission) {
        return true;
      }
      return this.auth.hasPermission(item.permission);
    }),
  );
}
