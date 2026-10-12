import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';

interface SettingsNavItem {
  id: string;
  labelKey: string;
  route: string;
  visible: boolean;
}

interface SettingsNavGroup {
  id: string;
  labelKey: string;
  items: SettingsNavItem[];
}

@Component({
  selector: 'kadr-settings-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe],
  template: `
    <div class="flex flex-col gap-6 lg:flex-row lg:items-start">
      <nav
        class="w-full shrink-0 lg:sticky lg:top-20 lg:w-56"
        [attr.aria-label]="'settings.navAria' | transloco"
      >
        <div class="flex flex-col gap-5">
          @for (group of visibleGroups(); track group.id) {
            <div class="flex flex-col gap-1">
              <p class="px-3 text-xs font-medium text-fg-muted">
                {{ group.labelKey | transloco }}
              </p>
              <ul class="flex flex-col gap-0.5">
                @for (item of group.items; track item.id) {
                  <li>
                    <a
                      [routerLink]="item.route"
                      routerLinkActive="bg-accent-soft text-accent-soft-fg border-accent"
                      class="flex min-h-10 items-center rounded-md border-s-2 border-transparent px-3 py-2 text-sm text-fg hover:bg-hover focus-visible:outline-none"
                    >
                      {{ item.labelKey | transloco }}
                    </a>
                  </li>
                }
              </ul>
            </div>
          }
        </div>
      </nav>
      <div class="min-w-0 flex-1">
        <router-outlet />
      </div>
    </div>
  `,
})
export class SettingsLayout {
  private readonly auth = inject(AuthService);

  private readonly groups = computed((): SettingsNavGroup[] => {
    const can = (code: string) => this.auth.hasPermission(code);
    return [
      {
        id: 'company',
        labelKey: 'settings.nav.groups.company',
        items: [
          {
            id: 'branding',
            labelKey: 'settings.nav.company',
            route: '/settings/company',
            visible: can(PermissionCodes.TenantSettingsRead),
          },
          {
            id: 'branches',
            labelKey: 'settings.nav.branches',
            route: '/settings/company/branches',
            visible: can(PermissionCodes.TenantSettingsRead),
          },
          {
            id: 'templates',
            labelKey: 'settings.nav.templates',
            route: '/settings/templates',
            visible: can(PermissionCodes.TenantTemplatesManage),
          },
        ],
      },
      {
        id: 'access',
        labelKey: 'settings.nav.groups.access',
        items: [
          {
            id: 'users',
            labelKey: 'settings.nav.users',
            route: '/settings/users',
            visible: can(PermissionCodes.UsersRead),
          },
          {
            id: 'invitations',
            labelKey: 'settings.nav.invitations',
            route: '/settings/invitations',
            visible: can(PermissionCodes.UsersRead),
          },
          {
            id: 'roles',
            labelKey: 'settings.nav.roles',
            route: '/settings/roles',
            visible: can(PermissionCodes.RolesRead),
          },
        ],
      },
      {
        id: 'timeOff',
        labelKey: 'settings.nav.groups.timeOff',
        items: [
          {
            id: 'leaveTypes',
            labelKey: 'settings.nav.leaveTypes',
            route: '/settings/leave-types',
            visible: can(PermissionCodes.LeaveManage),
          },
          {
            id: 'workflows',
            labelKey: 'settings.nav.workflows',
            route: '/settings/workflows',
            visible: can(PermissionCodes.WorkflowsManage),
          },
        ],
      },
      {
        id: 'billing',
        labelKey: 'settings.nav.groups.billing',
        items: [
          {
            id: 'subscription',
            labelKey: 'settings.nav.subscription',
            route: '/settings/subscription',
            visible: can(PermissionCodes.SubscriptionRead),
          },
        ],
      },
      {
        id: 'security',
        labelKey: 'settings.nav.groups.security',
        items: [
          {
            id: 'audit',
            labelKey: 'settings.nav.audit',
            route: '/settings/audit',
            visible: can(PermissionCodes.AuditRead),
          },
        ],
      },
    ];
  });

  protected readonly visibleGroups = computed(() =>
    this.groups()
      .map((g) => ({ ...g, items: g.items.filter((i) => i.visible) }))
      .filter((g) => g.items.length > 0),
  );
}
