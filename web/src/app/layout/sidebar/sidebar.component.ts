import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { FeatureKeys } from '../../core/subscription/feature-keys';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { IconComponent, IconName } from '../../shared/ui/icon/icon.component';
import { TenantLogoComponent } from '../../shared/ui/tenant-logo/tenant-logo.component';

export interface NavLeaf {
  kind: 'leaf';
  labelKey: string;
  icon: IconName;
  route?: string;
  disabled?: boolean;
  permission?: string;
  feature?: string;
  /** When set, show a numeric badge (approvals stub). */
  badge?: number;
  badgeAriaKey?: string;
}

export interface NavChild {
  labelKey: string;
  route: string;
  permission?: string;
  matchPrefix?: string;
}

export interface NavGroup {
  kind: 'group';
  id: string;
  labelKey: string;
  icon: IconName;
  permission?: string;
  feature?: string;
  /** Show group if any child is visible (ignores own permission when set). */
  anyChildPermission?: boolean;
  children: NavChild[];
}

export interface NavDivider {
  kind: 'divider';
}

export type NavEntry = NavLeaf | NavGroup | NavDivider;

@Component({
  selector: 'kadr-sidebar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, IconComponent, TenantLogoComponent],
  template: `
    <aside
      class="flex h-dvh flex-col border-e border-line bg-surface transition-[width] duration-150 ease-out motion-reduce:transition-none"
      [class.w-sidebar]="!collapsed()"
      [class.w-sidebar-collapsed]="collapsed()"
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
            <p
              class="truncate text-sm font-bold text-fg"
              [attr.title]="tenantName() || null"
            >
              {{ tenantName() || ('app.name' | transloco) }}
            </p>
          </div>
        }
        <button
          type="button"
          class="inline-flex size-11 shrink-0 items-center justify-center rounded-md text-fg-2 transition-colors duration-150 ease-out hover:bg-hover focus-visible:outline-none motion-reduce:transition-none"
          [attr.aria-label]="(collapsed() ? 'shell.expandSidebar' : 'shell.collapseSidebar') | transloco"
          [attr.aria-expanded]="!collapsed()"
          [attr.title]="(collapsed() ? 'shell.expandSidebar' : 'shell.collapseSidebar') | transloco"
          [attr.data-collapse-dir]="collapseDir()"
          (click)="collapsedChange.emit(!collapsed())"
        >
          <kadr-icon [name]="collapseIcon()" [rtlFlip]="true" size="sm" />
        </button>
      </div>

      <nav class="flex flex-1 flex-col gap-1 overflow-y-auto p-2">
        @for (entry of visibleEntries(); track trackEntry(entry, $index)) {
          @if (entry.kind === 'divider') {
            <div class="my-2 border-t border-line" role="separator"></div>
          } @else if (entry.kind === 'leaf') {
            @if (entry.disabled || !entry.route) {
              <span
                class="flex min-h-11 items-center gap-3 rounded-md px-3 py-2 text-sm text-fg-muted opacity-60"
                [attr.title]="'shell.notAvailableYet' | transloco"
              >
                <kadr-icon [name]="entry.icon" size="sm" />
                @if (!collapsed()) {
                  <span class="truncate">{{ entry.labelKey | transloco }}</span>
                }
              </span>
            } @else {
              <a
                [routerLink]="entry.route"
                class="flex min-h-11 items-center gap-3 rounded-md border-s-2 border-transparent px-3 py-2 text-sm text-fg transition-colors duration-150 ease-out hover:bg-hover focus-visible:outline-none motion-reduce:transition-none"
                [class.bg-accent-soft]="isLeafActive(entry)"
                [class.text-accent-soft-fg]="isLeafActive(entry)"
                [class.border-accent]="isLeafActive(entry)"
                [attr.title]="collapsed() ? (entry.labelKey | transloco) : null"
              >
                <kadr-icon [name]="entry.icon" size="sm" />
                @if (!collapsed()) {
                  <span class="min-w-0 flex-1 truncate">{{ entry.labelKey | transloco }}</span>
                  @if (entry.badge !== undefined) {
                    <span
                      class="inline-flex min-w-5 items-center justify-center rounded-md bg-subtle px-1.5 text-xs font-medium text-fg-2"
                      [attr.aria-label]="(entry.badgeAriaKey ?? 'shell.nav.approvalsPending') | transloco"
                    >
                      {{ entry.badge }}
                    </span>
                  }
                }
              </a>
            }
          } @else {
            @let open = isGroupOpen(entry);
            @let parentActive = isGroupActive(entry);
            <div class="flex flex-col gap-0.5">
              <button
                type="button"
                class="flex min-h-11 w-full items-center gap-3 rounded-md px-3 py-2 text-start text-sm transition-colors duration-150 ease-out focus-visible:outline-none motion-reduce:transition-none"
                [class.bg-accent-soft]="parentActive"
                [class.text-accent-soft-fg]="parentActive"
                [class.text-fg]="!parentActive"
                [class.hover:bg-hover]="!parentActive"
                [attr.aria-expanded]="open"
                [attr.title]="collapsed() ? (entry.labelKey | transloco) : null"
                (click)="toggleGroup(entry.id)"
              >
                <kadr-icon [name]="entry.icon" size="sm" />
                @if (!collapsed()) {
                  <span class="min-w-0 flex-1 truncate font-medium">{{ entry.labelKey | transloco }}</span>
                  <kadr-icon
                    name="chevron-down"
                    size="sm"
                    class="text-fg-muted transition-transform duration-150 ease-out motion-reduce:transition-none"
                    [class.rotate-180]="open"
                  />
                }
              </button>
              @if (open && !collapsed()) {
                <div class="flex flex-col gap-0.5 ps-3">
                  @for (child of visibleChildren(entry); track child.route) {
                    <a
                      [routerLink]="child.route"
                      class="flex min-h-10 items-center rounded-md border-s-2 border-transparent px-3 py-1.5 text-sm text-fg transition-colors duration-150 ease-out hover:bg-hover focus-visible:outline-none motion-reduce:transition-none"
                      [class.bg-accent-soft]="isChildActive(child)"
                      [class.text-accent-soft-fg]="isChildActive(child)"
                      [class.border-accent]="isChildActive(child)"
                    >
                      <span class="truncate">{{ child.labelKey | transloco }}</span>
                    </a>
                  }
                </div>
              }
            </div>
          }
        }
      </nav>

      @if (showTrialChip()) {
        <div class="border-t border-line p-2">
          @if (collapsed()) {
            <span
              class="flex min-h-11 items-center justify-center rounded-md bg-warning-soft text-xs font-medium text-fg"
              [attr.title]="'shell.trialDaysLeft' | transloco: { days: trialDaysLeft() }"
              [attr.aria-label]="'shell.trialDaysLeft' | transloco: { days: trialDaysLeft() }"
            >
              {{ trialDaysLeft() }}
            </span>
          } @else {
            <div class="rounded-md bg-warning-soft px-3 py-2 text-start text-xs font-medium text-fg">
              {{ 'shell.trialDaysLeft' | transloco: { days: trialDaysLeft() } }}
            </div>
          }
        </div>
      }
    </aside>
  `,
})
export class SidebarComponent {
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly router = inject(Router);

  readonly collapsed = input(false);
  readonly collapsedChange = output<boolean>();

  private readonly url = signal(this.router.url);
  private readonly manualOpen = signal<Record<string, boolean | undefined>>({});

  protected readonly tenantName = computed(() => this.auth.me()?.tenant.name ?? '');
  protected readonly tenantLogoUrl = computed(() => this.auth.me()?.tenant.logoUrl ?? null);
  protected readonly tenantLogoVersion = computed(() => this.tenantLogoUrl() ?? '');

  protected readonly collapseIcon = computed(() =>
    this.collapsed() ? 'chevron-end' : 'chevrons',
  );

  /** Logical direction the chevron points (after rtl-flip). */
  protected readonly collapseDir = computed(() => (this.collapsed() ? 'end' : 'start'));

  protected readonly trialDaysLeft = computed(() => {
    const sub = this.auth.me()?.subscription;
    if (!sub || sub.state !== 'trialRunning' || sub.daysLeft == null) {
      return null;
    }
    return sub.daysLeft;
  });

  protected readonly showTrialChip = computed(() => this.trialDaysLeft() != null);

  constructor() {
    this.router.events
      .pipe(
        filter((e): e is NavigationEnd => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((e) => this.url.set(e.urlAfterRedirects));
  }

  private readonly entries = computed((): NavEntry[] => {
    const canTeam =
      this.auth.hasPermission(PermissionCodes.AttendanceReadTeam) ||
      this.auth.hasPermission(PermissionCodes.AttendanceReadAll);
    const canReports = this.auth.hasPermission(PermissionCodes.AttendanceReports);
    const canManage = this.auth.hasPermission(PermissionCodes.AttendanceManage);

    const attendanceChildren: NavChild[] = [];
    if (canTeam) {
      attendanceChildren.push({
        labelKey: 'shell.nav.attendanceToday',
        route: '/attendance/today',
        matchPrefix: '/attendance/today',
      });
    }
    if (canManage) {
      attendanceChildren.push({
        labelKey: 'shell.nav.attendanceSchedules',
        route: '/attendance/schedules',
        matchPrefix: '/attendance/schedules',
      });
    }
    if (canReports) {
      attendanceChildren.push({
        labelKey: 'shell.nav.attendanceReports',
        route: '/attendance/reports',
        matchPrefix: '/attendance/reports',
      });
    }
    if (canManage) {
      attendanceChildren.push({
        labelKey: 'shell.nav.attendanceLocations',
        route: '/attendance/locations',
        matchPrefix: '/attendance/locations',
      });
      attendanceChildren.push({
        labelKey: 'shell.nav.attendanceRules',
        route: '/attendance/settings/rules',
        matchPrefix: '/attendance/settings/rules',
      });
      attendanceChildren.push({
        labelKey: 'shell.nav.attendanceBranches',
        route: '/attendance/settings/branches',
        matchPrefix: '/attendance/settings/branches',
      });
      attendanceChildren.push({
        labelKey: 'shell.nav.attendanceHolidays',
        route: '/attendance/settings/holidays',
        matchPrefix: '/attendance/settings/holidays',
      });
    }
    attendanceChildren.push({
      labelKey: 'shell.nav.attendanceMyLog',
      route: '/attendance/me',
      matchPrefix: '/attendance/me',
    });

    const peopleChildren: NavChild[] = [
      {
        labelKey: 'shell.nav.employees',
        route: '/employees',
        permission: PermissionCodes.EmployeesRead,
        matchPrefix: '/employees',
      },
      {
        labelKey: 'shell.nav.org',
        route: '/org',
        permission: PermissionCodes.OrgRead,
        matchPrefix: '/org',
      },
    ];

    const canLeaveTeam = this.auth.hasPermission(PermissionCodes.LeaveReadTeam);

    const leaveChildren: NavChild[] = [
      {
        labelKey: 'shell.nav.leaveMe',
        route: '/leave/me',
        matchPrefix: '/leave/me',
      },
      ...(canLeaveTeam
        ? [
            {
              labelKey: 'shell.nav.leaveApprovals',
              route: '/leave/approvals',
              permission: PermissionCodes.LeaveReadTeam,
              matchPrefix: '/leave/approvals',
            },
          ]
        : []),
      ...(this.auth.hasPermission(PermissionCodes.LeaveReadAll)
        ? [
            {
              labelKey: 'shell.nav.leaveBalances',
              route: '/leave/balances',
              matchPrefix: '/leave/balances',
            },
          ]
        : []),
    ];

    return [
      { kind: 'leaf', labelKey: 'shell.nav.home', icon: 'home', route: '/' },
      {
        kind: 'group',
        id: 'people',
        labelKey: 'shell.nav.people',
        icon: 'users',
        anyChildPermission: true,
        children: peopleChildren,
      },
      {
        kind: 'group',
        id: 'attendance',
        labelKey: 'shell.nav.attendance',
        icon: 'clock',
        permission: PermissionCodes.AttendanceReadOwn,
        children: attendanceChildren,
      },
      {
        kind: 'group',
        id: 'leave',
        labelKey: 'shell.nav.leave',
        icon: 'calendar',
        permission: PermissionCodes.LeaveReadOwn,
        feature: FeatureKeys.Leave,
        children: leaveChildren,
      },
      ...(canLeaveTeam
        ? []
        : [
            {
              kind: 'leaf' as const,
              labelKey: 'shell.nav.approvals',
              icon: 'inbox' as IconName,
              route: '/attendance/requests',
              permission: PermissionCodes.AttendanceReadOwn,
              badge: 0,
              badgeAriaKey: 'shell.nav.approvalsPending',
            },
          ]),
      {
        kind: 'group',
        id: 'payroll',
        labelKey: 'shell.nav.payroll',
        icon: 'file',
        anyChildPermission: true,
        children: [
          {
            labelKey: 'shell.nav.payrollRuns',
            route: '/payroll/runs',
            permission: PermissionCodes.PayrollRun,
            matchPrefix: '/payroll/runs',
          },
        ],
      },
      {
        kind: 'leaf',
        labelKey: 'shell.nav.reports',
        icon: 'chart',
        route: '/reports',
        permission: PermissionCodes.ReportsRead,
      },
      { kind: 'divider' },
      {
        kind: 'leaf',
        labelKey: 'shell.nav.settings',
        icon: 'settings',
        route: '/settings',
        permission: PermissionCodes.TenantSettingsRead,
      },
    ];
  });

  protected readonly visibleEntries = computed(() =>
    this.entries().filter((entry) => {
      if (entry.kind === 'divider') {
        return true;
      }
      if (entry.feature && !this.subscription.hasFeature(entry.feature)) {
        return false;
      }
      if (entry.kind === 'group' && entry.anyChildPermission) {
        return this.visibleChildren(entry).length > 0;
      }
      if (!entry.permission) {
        return true;
      }
      return this.auth.hasPermission(entry.permission);
    }),
  );

  protected trackEntry(entry: NavEntry, index: number): string {
    if (entry.kind === 'divider') {
      return `d:${index}`;
    }
    return entry.kind === 'group' ? `g:${entry.id}` : `l:${entry.labelKey}`;
  }

  protected visibleChildren(group: NavGroup): NavChild[] {
    return group.children.filter((c) => !c.permission || this.auth.hasPermission(c.permission));
  }

  protected isGroupActive(group: NavGroup): boolean {
    const path = this.url();
    return this.visibleChildren(group).some((c) => this.matches(path, c));
  }

  protected isGroupOpen(group: NavGroup): boolean {
    const manual = this.manualOpen()[group.id];
    if (manual !== undefined) {
      return manual;
    }
    return this.isGroupActive(group);
  }

  protected toggleGroup(id: string): void {
    if (this.collapsed()) {
      this.collapsedChange.emit(false);
    }
    const group = this.entries().find((e): e is NavGroup => e.kind === 'group' && e.id === id);
    if (!group) {
      return;
    }
    const next = !this.isGroupOpen(group);
    this.manualOpen.update((m) => ({ ...m, [id]: next }));
  }

  protected isChildActive(child: NavChild): boolean {
    return this.matches(this.url(), child);
  }

  protected isLeafActive(leaf: NavLeaf): boolean {
    if (!leaf.route) {
      return false;
    }
    const path = (this.url().split('?')[0] ?? this.url());
    if (leaf.route === '/') {
      return path === '/' || path === '';
    }
    if (leaf.route === '/settings') {
      return path === '/settings' || path.startsWith('/settings/');
    }
    return path === leaf.route || path.startsWith(leaf.route + '/');
  }

  private matches(url: string, child: NavChild): boolean {
    const path = url.split('?')[0] ?? url;
    const prefix = child.matchPrefix ?? child.route;
    return path === child.route || path.startsWith(prefix + '/') || path === prefix;
  }
}
