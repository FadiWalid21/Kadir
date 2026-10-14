import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { IconComponent, IconName } from '../../shared/ui/icon/icon.component';

interface MoreItem {
  labelKey: string;
  route: string;
  icon: IconName;
  permission?: string;
}

@Component({
  selector: 'kadr-bottom-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, RouterLinkActive, IconComponent],
  template: `
    <nav
      class="kadr-safe-bottom fixed inset-x-0 bottom-0 z-40 flex border-t border-line bg-surface md:hidden"
      [attr.aria-label]="'shell.bottomNav' | transloco"
    >
      <a
        routerLink="/home"
        routerLinkActive="text-accent"
        [routerLinkActiveOptions]="{ exact: true }"
        class="flex min-h-14 flex-1 flex-col items-center justify-center gap-1 py-2 text-xs text-fg-2 focus-visible:outline-none"
        (click)="moreOpen.set(false)"
      >
        <kadr-icon name="home" size="sm" />
        {{ 'shell.nav.home' | transloco }}
      </a>
      <a
        [routerLink]="approvalsRoute()"
        routerLinkActive="text-accent"
        class="flex min-h-14 flex-1 flex-col items-center justify-center gap-1 py-2 text-xs text-fg-2 focus-visible:outline-none"
        (click)="moreOpen.set(false)"
      >
        <kadr-icon name="inbox" size="sm" />
        {{ approvalsLabelKey() | transloco }}
      </a>
      <a
        routerLink="/attendance/me"
        routerLinkActive="text-accent"
        class="flex min-h-14 flex-1 flex-col items-center justify-center gap-1 py-2 text-xs text-fg-2 focus-visible:outline-none"
        (click)="moreOpen.set(false)"
      >
        <kadr-icon name="clock" size="sm" />
        {{ 'shell.nav.attendance' | transloco }}
      </a>
      <button
        type="button"
        class="flex min-h-14 flex-1 flex-col items-center justify-center gap-1 py-2 text-xs focus-visible:outline-none"
        [class.text-accent]="moreOpen()"
        [class.text-fg-2]="!moreOpen()"
        [attr.aria-expanded]="moreOpen()"
        [attr.aria-controls]="'shell-more-sheet'"
        (click)="moreOpen.set(!moreOpen())"
      >
        <kadr-icon name="more-horizontal" size="sm" />
        {{ 'shell.nav.more' | transloco }}
      </button>
    </nav>

    @if (moreOpen()) {
      <div class="fixed inset-0 z-50 md:hidden" role="presentation">
        <button
          type="button"
          class="absolute inset-0 bg-ink/40"
          [attr.aria-label]="'ui.close' | transloco"
          (click)="moreOpen.set(false)"
        ></button>
        <div
          id="shell-more-sheet"
          class="kadr-safe-bottom absolute inset-x-0 bottom-0 rounded-t-lg border-t border-line bg-surface p-4 shadow-md"
          role="dialog"
          [attr.aria-label]="'shell.nav.more' | transloco"
        >
          <ul class="flex flex-col gap-1 pb-14">
            @for (item of moreItems(); track item.route) {
              <li>
                <a
                  [routerLink]="item.route"
                  class="flex min-h-11 items-center gap-3 rounded-md px-3 py-2 text-sm text-fg hover:bg-hover focus-visible:outline-none"
                  (click)="moreOpen.set(false)"
                >
                  <kadr-icon [name]="item.icon" size="sm" />
                  {{ item.labelKey | transloco }}
                </a>
              </li>
            }
          </ul>
        </div>
      </div>
    }
  `,
})
export class BottomNavComponent {
  private readonly auth = inject(AuthService);
  protected readonly moreOpen = signal(false);

  protected readonly canApprove = computed(() =>
    this.auth.hasPermission(PermissionCodes.LeaveReadTeam),
  );

  protected readonly approvalsRoute = computed(() =>
    this.canApprove() ? '/leave/approvals' : '/attendance/requests',
  );

  protected readonly approvalsLabelKey = computed(() =>
    this.canApprove() ? 'shell.nav.approvals' : 'shell.nav.requests',
  );

  protected readonly moreItems = computed((): MoreItem[] => {
    const items: MoreItem[] = [];
    if (
      this.auth.hasPermission(PermissionCodes.EmployeesRead) ||
      this.auth.hasPermission(PermissionCodes.OrgRead)
    ) {
      if (this.auth.hasPermission(PermissionCodes.EmployeesRead)) {
        items.push({
          labelKey: 'shell.nav.employees',
          route: '/employees',
          icon: 'users',
          permission: PermissionCodes.EmployeesRead,
        });
      }
      if (this.auth.hasPermission(PermissionCodes.OrgRead)) {
        items.push({
          labelKey: 'shell.nav.org',
          route: '/org',
          icon: 'building',
          permission: PermissionCodes.OrgRead,
        });
      }
    }
    if (this.auth.hasPermission(PermissionCodes.LeaveReadOwn)) {
      items.push({
        labelKey: 'shell.nav.leave',
        route: '/leave/me',
        icon: 'calendar',
        permission: PermissionCodes.LeaveReadOwn,
      });
    }
    if (this.auth.hasPermission(PermissionCodes.TenantSettingsRead)) {
      items.push({
        labelKey: 'shell.nav.settings',
        route: '/settings',
        icon: 'settings',
        permission: PermissionCodes.TenantSettingsRead,
      });
    }
    return items;
  });
}
