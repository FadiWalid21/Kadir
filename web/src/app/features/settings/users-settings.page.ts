import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { EmployeesApi } from '../../core/api/employees.api';
import { UserListItem } from '../../core/api/users.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { DirtyFormRegistry } from '../../core/forms/dirty-form.registry';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { DropdownMenuComponent } from '../../shared/ui/dropdown-menu/dropdown-menu.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { SelectComponent, SelectOption } from '../../shared/ui/select/select.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { TableComponent, TableColumn } from '../../shared/ui/table/table.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { UsersStore } from './users.store';

@Component({
  selector: 'kadr-users-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [UsersStore],
  imports: [
    TranslocoPipe,
    TableComponent,
    DrawerComponent,
    ButtonComponent,
    InputComponent,
    SelectComponent,
    BadgeComponent,
    AvatarComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DropdownMenuComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-5">
      <header class="text-start">
        <h1 class="text-2xl font-bold leading-tight text-fg">{{ 'settings.users.title' | transloco }}</h1>
        <p class="mt-1 max-w-2xl text-sm text-fg-2">{{ 'settings.users.subtitle' | transloco }}</p>
        <p class="mt-2 max-w-2xl text-sm text-fg-muted">{{ 'settings.users.linkHint' | transloco }}</p>
      </header>

      <div class="flex flex-col gap-3 lg:flex-row lg:items-end">
          <div class="min-w-0 flex-1">
            <kadr-input
              [label]="'settings.users.search' | transloco"
              [placeholder]="'settings.users.search' | transloco"
              [value]="store.q()"
              (valueChange)="onSearch($event)"
            />
          </div>
          <div class="grid w-full grid-cols-1 gap-3 sm:grid-cols-2 lg:w-auto lg:min-w-80 lg:grid-cols-2">
            <kadr-select
              [label]="'settings.users.filterRole' | transloco"
              [options]="roleOptions()"
              [value]="store.roleFilter()"
              (valueChange)="onRoleFilter($event)"
            />
            <kadr-select
              [label]="'settings.users.filterStatus' | transloco"
              [options]="statusOptions()"
              [value]="store.activeFilter()"
              (valueChange)="onStatusFilter($event)"
            />
          </div>
        </div>

        @if (store.listState() === 'loading') {
          <kadr-skeleton height="12rem" />
        } @else if (store.listState() === 'error') {
          <kadr-empty-state
            [title]="'settings.users.loadErrorTitle' | transloco"
            [description]="'settings.users.loadErrorBody' | transloco"
          >
            <kadr-button variant="secondary" type="button" (click)="reload()">{{ 'home.retry' | transloco }}</kadr-button>
          </kadr-empty-state>
        } @else if ((store.users()?.items.length ?? 0) === 0) {
          <kadr-empty-state
            [title]="'settings.users.emptyTitle' | transloco"
            [description]="'settings.users.emptyBody' | transloco"
          />
        } @else {
          <div class="hidden md:block">
            <kadr-table [columns]="userColumns()">
              @for (u of store.users()!.items; track u.id) {
                <tr class="h-row border-b border-line last:border-b-0">
                  <td class="px-3 py-2">
                    <div class="flex items-center gap-2">
                      <kadr-avatar [name]="u.fullName" size="sm" />
                      <div class="min-w-0 text-start">
                        <p class="truncate font-medium text-fg">{{ u.fullName }}</p>
                        <p class="truncate text-xs text-fg-2" dir="ltr">{{ u.email }}</p>
                      </div>
                    </div>
                  </td>
                  <td class="px-3 py-2">
                    <kadr-badge tone="neutral" [label]="u.roleName" />
                  </td>
                  <td class="px-3 py-2 text-sm text-fg-2">
                    {{ u.employeeNameAr || ('settings.users.notLinked' | transloco) }}
                  </td>
                  <td class="px-3 py-2">
                    <kadr-badge
                      [tone]="u.isActive ? 'success' : 'warning'"
                      [label]="(u.isActive ? 'settings.users.active' : 'settings.users.inactive') | transloco"
                    />
                  </td>
                  <td class="px-3 py-2 text-xs text-fg-2">{{ formatDate(u.lastLoginAt) }}</td>
                  <td class="px-3 py-2">
                    @if (canUpdate()) {
                      <kadr-dropdown-menu [ariaLabel]="'settings.users.rowActions' | transloco">
                        <span dropdownTrigger class="inline-flex size-8 items-center justify-center rounded-md hover:bg-hover">
                          <svg viewBox="0 0 24 24" class="size-4" fill="currentColor" aria-hidden="true">
                            <circle cx="12" cy="5" r="1.5" />
                            <circle cx="12" cy="12" r="1.5" />
                            <circle cx="12" cy="19" r="1.5" />
                          </svg>
                        </span>
                        <div dropdownContent>
                          @if (!isOwnerRow(u)) {
                            <button type="button" class="block w-full px-3 py-2 text-start text-sm hover:bg-hover" role="menuitem" (click)="openEdit(u)">
                              {{ 'settings.users.edit' | transloco }}
                            </button>
                          }
                          <button type="button" class="block w-full px-3 py-2 text-start text-sm hover:bg-hover" role="menuitem" (click)="openLink(u)">
                            {{ (u.employeeId ? 'settings.users.changeLink' : 'settings.users.linkEmployee') | transloco }}
                          </button>
                          @if (u.employeeId) {
                            <button type="button" class="block w-full px-3 py-2 text-start text-sm hover:bg-hover" role="menuitem" (click)="unlinkEmployee(u)">
                              {{ 'settings.users.unlinkEmployee' | transloco }}
                            </button>
                          }
                          @if (!isOwnerRow(u)) {
                            <button
                              type="button"
                              class="block w-full px-3 py-2 text-start text-sm hover:bg-hover"
                              role="menuitem"
                              (click)="toggleActive(u)"
                            >
                              {{ (u.isActive ? 'settings.users.deactivate' : 'settings.users.reactivate') | transloco }}
                            </button>
                          }
                        </div>
                      </kadr-dropdown-menu>
                    } @else if (isOwnerRow(u)) {
                      <span class="inline-flex size-8 items-center justify-center text-fg-muted" [attr.aria-label]="'settings.users.ownerLocked' | transloco">
                        <svg viewBox="0 0 24 24" class="size-4" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                          <rect x="5" y="11" width="14" height="10" rx="2" />
                          <path d="M8 11V8a4 4 0 0 1 8 0v3" />
                        </svg>
                      </span>
                    }
                  </td>
                </tr>
              }
            </kadr-table>
          </div>
          <div class="flex flex-col gap-3 md:hidden">
            @for (u of store.users()!.items; track u.id) {
              <article class="rounded-md border border-line bg-surface p-3 text-start">
                <div class="flex items-center gap-2">
                  <kadr-avatar [name]="u.fullName" size="sm" />
                  <div class="min-w-0 flex-1">
                    <p class="truncate font-medium text-fg">{{ u.fullName }}</p>
                    <p class="truncate text-xs text-fg-2" dir="ltr">{{ u.email }}</p>
                  </div>
                </div>
                <div class="mt-2 flex flex-wrap gap-2">
                  <kadr-badge tone="neutral" [label]="u.roleName" />
                  <kadr-badge
                    [tone]="u.isActive ? 'success' : 'warning'"
                    [label]="(u.isActive ? 'settings.users.active' : 'settings.users.inactive') | transloco"
                  />
                </div>
                <p class="mt-2 text-xs text-fg-2">
                  {{ 'settings.users.colEmployee' | transloco }}:
                  {{ u.employeeNameAr || ('settings.users.notLinked' | transloco) }}
                </p>
                @if (canUpdate()) {
                  <div class="mt-2 flex flex-wrap gap-2">
                    <kadr-button variant="secondary" type="button" (click)="openLink(u)">
                      {{ (u.employeeId ? 'settings.users.changeLink' : 'settings.users.linkEmployee') | transloco }}
                    </kadr-button>
                  </div>
                }
              </article>
            }
          </div>
          <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p class="text-xs text-fg-2">
              {{
                'settings.users.paginationRange'
                  | transloco
                    : {
                        from: pageFrom(),
                        to: pageTo(),
                        total: store.users()!.totalCount,
                      }
              }}
            </p>
            <div class="flex items-center gap-1">
              <button
                type="button"
                class="inline-flex size-8 items-center justify-center rounded-md border border-line text-fg-2 hover:bg-hover disabled:opacity-40"
                [disabled]="store.users()!.page <= 1"
                [attr.aria-label]="'settings.users.prevPage' | transloco"
                (click)="goPage(store.users()!.page - 1)"
              >
                <svg viewBox="0 0 24 24" class="size-4 rtl-flip" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                  <path d="m15 18-6-6 6-6" />
                </svg>
              </button>
              <span class="inline-flex size-8 items-center justify-center rounded-md bg-accent text-sm font-medium text-on-accent">
                {{ store.users()!.page }}
              </span>
              <button
                type="button"
                class="inline-flex size-8 items-center justify-center rounded-md border border-line text-fg-2 hover:bg-hover disabled:opacity-40"
                [disabled]="store.users()!.page * store.users()!.pageSize >= store.users()!.totalCount"
                [attr.aria-label]="'settings.users.nextPage' | transloco"
                (click)="goPage(store.users()!.page + 1)"
              >
                <svg viewBox="0 0 24 24" class="size-4 rtl-flip" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                  <path d="m9 18 6-6-6-6" />
                </svg>
              </button>
            </div>
          </div>
        }

      <kadr-drawer
        [open]="!!editUser()"
        [title]="'settings.users.editTitle' | transloco"
        [closeLabel]="'ui.cancel' | transloco"
        (closed)="editUser.set(null)"
      >
        <kadr-input
          [label]="'settings.users.fullName' | transloco"
          [value]="editName()"
          (valueChange)="editName.set($event)"
        />
        <div class="mt-4">
          <kadr-select
            [label]="'settings.users.filterRole' | transloco"
            [options]="assignableRoleOptions()"
            [value]="editRoleId()"
            (valueChange)="editRoleId.set($event)"
          />
        </div>
        <div drawerFooter class="contents">
          <kadr-button variant="primary" type="button" [loading]="store.busy()" (click)="saveEdit()">{{ 'ui.save' | transloco }}</kadr-button>
          <kadr-button variant="secondary" type="button" (click)="editUser.set(null)">{{ 'ui.cancel' | transloco }}</kadr-button>
        </div>
      </kadr-drawer>

      <kadr-drawer
        [open]="!!linkUser()"
        [title]="'settings.users.linkTitle' | transloco"
        [closeLabel]="'ui.cancel' | transloco"
        (closed)="linkUser.set(null)"
      >
        <p class="mb-3 text-sm text-fg-2">{{ 'settings.users.linkBody' | transloco }}</p>
        @if (linkUser()?.employeeNameAr) {
          <p class="mb-3 text-sm text-fg">
            {{ 'settings.users.currentlyLinked' | transloco: { name: linkUser()!.employeeNameAr } }}
          </p>
        }
        <kadr-select
          [label]="'settings.users.employee' | transloco"
          [options]="employeeOptions()"
          [value]="linkEmployeeId()"
          (valueChange)="linkEmployeeId.set($event)"
        />
        <div drawerFooter class="contents">
          <kadr-button variant="primary" type="button" [loading]="store.busy()" (click)="saveLink()">
            {{ 'settings.users.saveLink' | transloco }}
          </kadr-button>
          <kadr-button variant="secondary" type="button" (click)="linkUser.set(null)">{{ 'ui.cancel' | transloco }}</kadr-button>
        </div>
      </kadr-drawer>
    </div>
  `,
})
export class UsersSettingsPage implements OnInit {
  protected readonly store = inject(UsersStore);
  private readonly employeesApi = inject(EmployeesApi);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(TranslocoService);
  private readonly dirtyForms = inject(DirtyFormRegistry);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly editUser = signal<UserListItem | null>(null);
  protected readonly editName = signal('');
  protected readonly editRoleId = signal('');
  protected readonly linkUser = signal<UserListItem | null>(null);
  protected readonly linkEmployeeId = signal('');
  protected readonly employeeOptions = signal<SelectOption[]>([]);

  protected readonly readOnly = computed(() => this.subscription.readOnly());
  protected readonly canUpdate = computed(
    () => this.auth.hasPermission(PermissionCodes.UsersUpdate) && !this.readOnly(),
  );
  protected readonly userColumns = computed<TableColumn[]>(() => [
    { id: 'user', label: this.i18n.translate('settings.users.colUser') },
    { id: 'role', label: this.i18n.translate('settings.users.colRole') },
    { id: 'employee', label: this.i18n.translate('settings.users.colEmployee') },
    { id: 'status', label: this.i18n.translate('settings.users.colStatus') },
    { id: 'login', label: this.i18n.translate('settings.users.colLastLogin') },
    { id: 'actions', label: this.i18n.translate('settings.users.colActions') },
  ]);

  protected readonly pageFrom = computed(() => {
    const page = this.store.users();
    if (!page || page.totalCount === 0) {
      return 0;
    }
    return (page.page - 1) * page.pageSize + 1;
  });

  protected readonly pageTo = computed(() => {
    const page = this.store.users();
    if (!page) {
      return 0;
    }
    return Math.min(page.page * page.pageSize, page.totalCount);
  });

  protected readonly roleOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('settings.users.allRoles') },
    ...this.store.roles().map((r) => ({ value: r.id, label: r.name })),
  ]);

  protected readonly assignableRoleOptions = computed<SelectOption[]>(() =>
    this.store.roles()
      .filter((r) => r.name !== 'Owner')
      .map((r) => ({ value: r.id, label: r.name })),
  );

  protected readonly statusOptions = computed<SelectOption[]>(() => [
    { value: 'all', label: this.i18n.translate('settings.users.allStatuses') },
    { value: 'true', label: this.i18n.translate('settings.users.active') },
    { value: 'false', label: this.i18n.translate('settings.users.inactive') },
  ]);

  constructor() {
    effect(() => {
      const user = this.editUser();
      const dirty =
        !!user &&
        (this.editName().trim() !== user.fullName || this.editRoleId() !== user.roleId);
      if (dirty) {
        this.dirtyForms.register('users-edit-drawer');
      } else {
        this.dirtyForms.clear('users-edit-drawer');
      }
    });
  }

  async ngOnInit(): Promise<void> {
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab === 'invites') {
      await this.router.navigate(['/settings/invitations'], { replaceUrl: true });
      return;
    }
    await this.store.loadRoles();
    await this.store.loadUsers();
  }

  protected reload(): void {
    void this.store.loadUsers();
  }

  protected onSearch(q: string): void {
    this.store.q.set(q);
    this.store.page.set(1);
    void this.store.loadUsers();
  }

  protected onRoleFilter(roleId: string): void {
    this.store.roleFilter.set(roleId);
    this.store.page.set(1);
    void this.store.loadUsers();
  }

  protected onStatusFilter(value: string): void {
    this.store.activeFilter.set(value as 'all' | 'true' | 'false');
    this.store.page.set(1);
    void this.store.loadUsers();
  }

  protected goPage(page: number): void {
    this.store.page.set(page);
    void this.store.loadUsers();
  }

  protected isOwnerRow(u: UserListItem): boolean {
    return u.roleName === 'Owner';
  }

  protected formatDate(value: string | null): string {
    if (!value) {
      return '—';
    }
    return new Date(value).toLocaleDateString();
  }

  protected openEdit(u: UserListItem): void {
    this.editUser.set(u);
    this.editName.set(u.fullName);
    this.editRoleId.set(u.roleId);
  }

  protected async openLink(u: UserListItem): Promise<void> {
    this.linkUser.set(u);
    this.linkEmployeeId.set(u.employeeId ?? '');
    try {
      const page = await this.employeesApi.list({ page: 1, pageSize: 100 });
      const lang = this.i18n.getActiveLang();
      this.employeeOptions.set([
        { value: '', label: this.i18n.translate('settings.users.pickEmployee') },
        ...page.items
          .filter((e) => e.status === 'Active' || e.status === 'Probation')
          .map((e) => ({
            value: e.id,
            label:
              lang === 'en' && e.fullNameEn
                ? `${e.fullNameEn} (${e.employeeNumber})`
                : `${e.fullNameAr} (${e.employeeNumber})`,
          })),
      ]);
    } catch {
      this.employeeOptions.set([{ value: '', label: this.i18n.translate('settings.users.pickEmployee') }]);
      this.toast.show(this.i18n.translate('settings.users.linkFailed'), 'danger');
    }
  }

  protected async saveLink(): Promise<void> {
    const u = this.linkUser();
    const employeeId = this.linkEmployeeId().trim();
    if (!u || !employeeId) {
      this.toast.show(this.i18n.translate('settings.users.pickEmployeeRequired'), 'danger');
      return;
    }
    if (u.employeeId && u.employeeId !== employeeId) {
      try {
        await this.store.setEmployeeLink(u.id, null);
      } catch {
        this.toast.show(this.i18n.translate('settings.users.linkFailed'), 'danger');
        return;
      }
    }
    try {
      await this.store.setEmployeeLink(u.id, employeeId);
      this.linkUser.set(null);
      this.toast.show(this.i18n.translate('settings.users.linked'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.users.linkFailed'), 'danger');
    }
  }

  protected async unlinkEmployee(u: UserListItem): Promise<void> {
    try {
      await this.store.setEmployeeLink(u.id, null);
      this.toast.show(this.i18n.translate('settings.users.unlinked'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.users.linkFailed'), 'danger');
    }
  }

  protected async saveEdit(): Promise<void> {
    const u = this.editUser();
    if (!u) {
      return;
    }
    try {
      await this.store.updateUser(u.id, {
        fullName: this.editName().trim(),
        roleId: this.editRoleId(),
        isActive: u.isActive,
        dataScope: typeof u.dataScope === 'number' ? u.dataScope : 4,
      });
      this.editUser.set(null);
      this.toast.show(this.i18n.translate('settings.users.updated'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.users.updateFailed'), 'danger');
    }
  }

  protected async toggleActive(u: UserListItem): Promise<void> {
    try {
      await this.store.updateUser(u.id, {
        fullName: u.fullName,
        roleId: u.roleId,
        isActive: !u.isActive,
        dataScope: typeof u.dataScope === 'number' ? u.dataScope : 4,
      });
      this.toast.show(
        this.i18n.translate(u.isActive ? 'settings.users.deactivated' : 'settings.users.reactivated'),
        'success',
      );
    } catch {
      this.toast.show(this.i18n.translate('settings.users.updateFailed'), 'danger');
    }
  }

}
