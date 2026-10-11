import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { InvitationListItem, UserListItem } from '../../core/api/users.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { DropdownMenuComponent } from '../../shared/ui/dropdown-menu/dropdown-menu.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { RadioCardComponent } from '../../shared/ui/radio-card/radio-card.component';
import { SelectComponent, SelectOption } from '../../shared/ui/select/select.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { TableComponent, TableColumn } from '../../shared/ui/table/table.component';
import { TabsComponent, TabItem } from '../../shared/ui/tabs/tabs.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { SettingsSubnavComponent } from './settings-subnav.component';
import { UsersStore } from './users.store';

@Component({
  selector: 'kadr-users-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [UsersStore],
  imports: [
    TranslocoPipe,
    SettingsSubnavComponent,
    TabsComponent,
    TableComponent,
    DrawerComponent,
    ButtonComponent,
    InputComponent,
    SelectComponent,
    RadioCardComponent,
    BadgeComponent,
    AvatarComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DropdownMenuComponent,
    ConfirmDialogComponent,
    IconComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-5">
      <kadr-settings-subnav active="users" [showTemplates]="canTemplates()" />

      <header class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div class="min-w-0 flex-1 text-start">
          <h1 class="text-2xl font-bold leading-tight text-fg">{{ 'settings.users.title' | transloco }}</h1>
          <p class="mt-1 max-w-2xl text-sm text-fg-2">{{ 'settings.users.subtitle' | transloco }}</p>
        </div>
        @if (canInvitePerm()) {
          <span
            class="inline-flex shrink-0"
            [attr.title]="readOnly() ? ('subscription.readOnlyHint' | transloco) : null"
          >
            <kadr-button
              variant="primary"
              type="button"
              [disabled]="readOnly()"
              (click)="openInvite()"
            >
              <span class="inline-flex items-center gap-2">
                @if (readOnly()) {
                  <kadr-icon name="lock" size="sm" />
                } @else {
                  <svg viewBox="0 0 24 24" class="size-4" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                    <path d="M12 5v14" />
                    <path d="M5 12h14" />
                  </svg>
                }
                {{ 'settings.users.invite' | transloco }}
              </span>
            </kadr-button>
          </span>
        }
      </header>

      <kadr-tabs [tabs]="tabs()" [ariaLabel]="'settings.users.tabsAria' | transloco" [(activeId)]="activeTab" />

      @if (activeTab() === 'users') {
        <div class="flex flex-col gap-3 lg:flex-row lg:items-end">
          <div class="min-w-0 flex-1">
            <kadr-input
              [label]="'settings.users.search' | transloco"
              [placeholder]="'settings.users.search' | transloco"
              [value]="store.q()"
              (valueChange)="onSearch($event)"
            />
          </div>
          <div class="grid w-full grid-cols-1 gap-3 sm:grid-cols-2 lg:w-auto lg:min-w-[20rem] lg:grid-cols-2">
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
                  <td class="px-3 py-2">
                    <kadr-badge
                      [tone]="u.isActive ? 'success' : 'warning'"
                      [label]="(u.isActive ? 'settings.users.active' : 'settings.users.inactive') | transloco"
                    />
                  </td>
                  <td class="px-3 py-2 text-xs text-fg-2">{{ formatDate(u.lastLoginAt) }}</td>
                  <td class="px-3 py-2">
                    @if (isOwnerRow(u)) {
                      <span class="inline-flex size-8 items-center justify-center text-fg-muted" [attr.aria-label]="'settings.users.ownerLocked' | transloco">
                        <svg viewBox="0 0 24 24" class="size-4" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                          <rect x="5" y="11" width="14" height="10" rx="2" />
                          <path d="M8 11V8a4 4 0 0 1 8 0v3" />
                        </svg>
                      </span>
                    } @else if (canUpdate()) {
                      <kadr-dropdown-menu [ariaLabel]="'settings.users.rowActions' | transloco">
                        <span dropdownTrigger class="inline-flex size-8 items-center justify-center rounded-md hover:bg-hover">
                          <svg viewBox="0 0 24 24" class="size-4" fill="currentColor" aria-hidden="true">
                            <circle cx="12" cy="5" r="1.5" />
                            <circle cx="12" cy="12" r="1.5" />
                            <circle cx="12" cy="19" r="1.5" />
                          </svg>
                        </span>
                        <div dropdownContent>
                          <button type="button" class="block w-full px-3 py-2 text-start text-sm hover:bg-hover" role="menuitem" (click)="openEdit(u)">
                            {{ 'settings.users.edit' | transloco }}
                          </button>
                          <button
                            type="button"
                            class="block w-full px-3 py-2 text-start text-sm hover:bg-hover"
                            role="menuitem"
                            (click)="toggleActive(u)"
                          >
                            {{ (u.isActive ? 'settings.users.deactivate' : 'settings.users.reactivate') | transloco }}
                          </button>
                        </div>
                      </kadr-dropdown-menu>
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
      } @else {
        @if (store.invitesState() === 'loading') {
          <kadr-skeleton height="8rem" />
        } @else if (store.invitesState() === 'error') {
          <kadr-empty-state [title]="'settings.users.invitesErrorTitle' | transloco" [description]="'settings.users.loadErrorBody' | transloco">
            <kadr-button variant="secondary" type="button" (click)="store.loadInvitations()">{{ 'home.retry' | transloco }}</kadr-button>
          </kadr-empty-state>
        } @else if (store.invitations().length === 0) {
          <kadr-empty-state
            [title]="'settings.users.invitesEmptyTitle' | transloco"
            [description]="'settings.users.invitesEmptyBody' | transloco"
          />
        } @else {
          <ul class="flex flex-col gap-2">
            @for (inv of store.invitations(); track inv.id) {
              <li class="flex flex-col gap-2 rounded-md border border-line bg-surface p-3 sm:flex-row sm:items-center sm:justify-between">
                <div class="min-w-0 text-start">
                  <p class="truncate font-medium text-fg" dir="ltr">{{ inv.email }}</p>
                  <div class="mt-1 flex flex-wrap items-center gap-2">
                    <kadr-badge tone="warning" [label]="'settings.users.pending' | transloco" />
                    <kadr-badge tone="neutral" [label]="inv.roleName" />
                  </div>
                </div>
                @if (canInvite()) {
                  <div class="flex flex-wrap gap-2">
                    <kadr-button variant="secondary" size="sm" type="button" [loading]="store.busy()" (click)="resend(inv)">
                      {{ 'settings.users.resend' | transloco }}
                    </kadr-button>
                    <kadr-button variant="danger-soft" size="sm" type="button" (click)="askRevoke(inv)">
                      {{ 'settings.users.revoke' | transloco }}
                    </kadr-button>
                  </div>
                }
              </li>
            }
          </ul>
        }
      }

      <kadr-drawer
        [open]="inviteOpen()"
        size="lg"
        [title]="'settings.users.inviteTitle' | transloco"
        [subtitle]="'settings.users.inviteSubtitle' | transloco"
        [closeLabel]="'ui.cancel' | transloco"
        (closed)="inviteOpen.set(false)"
      >
        <kadr-input
          [label]="'settings.users.inviteEmailLabel' | transloco"
          type="email"
          [required]="true"
          [value]="inviteEmail()"
          [error]="inviteError() ?? undefined"
          [hint]="'settings.users.inviteEmailHint' | transloco"
          (valueChange)="inviteEmail.set($event); inviteError.set(null)"
        />
        <p class="mb-2 mt-5 text-sm font-medium text-fg">
          {{ 'settings.users.inviteRole' | transloco }}
          <span class="text-danger" aria-hidden="true"> *</span>
        </p>
        <div class="flex flex-col gap-2" role="radiogroup" [attr.aria-label]="'settings.users.inviteRole' | transloco">
          @for (role of store.roles(); track role.id) {
            <kadr-radio-card
              [title]="role.name"
              [description]="roleDescription(role.descriptionAr, role.descriptionEn)"
              [selected]="inviteRoleId() === role.id"
              [disabled]="role.name === 'Owner'"
              [disabledNote]="role.name === 'Owner' ? ('settings.users.ownerCannotGrant' | transloco) : undefined"
              (selectedChange)="inviteRoleId.set(role.id)"
            />
          }
        </div>
        <div
          class="mt-5 flex items-start gap-2 rounded-md border border-line bg-info-soft px-3 py-2.5 text-start"
          role="note"
        >
          <svg viewBox="0 0 24 24" class="mt-0.5 size-4 shrink-0 text-info" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <circle cx="12" cy="12" r="10" />
            <path d="M12 16v-4" />
            <path d="M12 8h.01" />
          </svg>
          <p class="text-xs text-fg-2">{{ 'settings.users.inviteExpiryNote' | transloco }}</p>
        </div>
        <div drawerFooter class="contents">
          <kadr-button variant="primary" type="button" [loading]="store.busy()" (click)="submitInvite()">
            {{ 'settings.users.sendInvite' | transloco }}
          </kadr-button>
          <kadr-button variant="secondary" type="button" (click)="inviteOpen.set(false)">{{ 'ui.cancel' | transloco }}</kadr-button>
        </div>
      </kadr-drawer>

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

      <kadr-confirm-dialog
        [open]="!!revokeTarget()"
        [title]="'settings.users.revokeTitle' | transloco"
        [message]="'settings.users.revokeBody' | transloco"
        [confirmLabel]="'settings.users.revoke' | transloco"
        [cancelLabel]="'ui.cancel' | transloco"
        [danger]="true"
        [busy]="store.busy()"
        (confirmed)="confirmRevoke()"
        (cancelled)="revokeTarget.set(null)"
      />
    </div>
  `,
})
export class UsersSettingsPage implements OnInit {
  protected readonly store = inject(UsersStore);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(TranslocoService);

  protected readonly activeTab = signal('users');
  protected readonly inviteOpen = signal(false);
  protected readonly inviteEmail = signal('');
  protected readonly inviteRoleId = signal('');
  protected readonly inviteError = signal<string | null>(null);
  protected readonly editUser = signal<UserListItem | null>(null);
  protected readonly editName = signal('');
  protected readonly editRoleId = signal('');
  protected readonly revokeTarget = signal<InvitationListItem | null>(null);

  protected readonly readOnly = computed(() => this.subscription.readOnly());
  protected readonly canInvitePerm = computed(() =>
    this.auth.hasPermission(PermissionCodes.UsersInvite),
  );
  protected readonly canInvite = computed(() => this.canInvitePerm() && !this.readOnly());
  protected readonly canUpdate = computed(
    () => this.auth.hasPermission(PermissionCodes.UsersUpdate) && !this.readOnly(),
  );
  protected readonly canTemplates = computed(() =>
    this.auth.hasPermission(PermissionCodes.TenantTemplatesManage),
  );

  protected readonly tabs = computed<TabItem[]>(() => [
    {
      id: 'users',
      label: this.i18n.translate('settings.users.tabUsers', {
        count: this.store.users()?.totalCount ?? 0,
      }),
    },
    {
      id: 'invites',
      label: this.i18n.translate('settings.users.tabInvites', {
        count: this.store.invitations().length,
      }),
    },
  ]);

  protected readonly userColumns = computed<TableColumn[]>(() => [
    { id: 'user', label: this.i18n.translate('settings.users.colUser') },
    { id: 'role', label: this.i18n.translate('settings.users.colRole') },
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

  async ngOnInit(): Promise<void> {
    await this.store.loadRoles();
    await Promise.all([this.store.loadUsers(), this.store.loadInvitations()]);
    const firstAssignable = this.store.roles().find((r) => r.name !== 'Owner');
    if (firstAssignable) {
      this.inviteRoleId.set(firstAssignable.id);
    }
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

  protected roleDescription(ar: string | null, en: string | null): string | undefined {
    const lang = this.i18n.getActiveLang();
    return (lang === 'ar' ? ar : en) ?? ar ?? en ?? undefined;
  }

  protected openInvite(): void {
    this.inviteEmail.set('');
    this.inviteError.set(null);
    this.inviteOpen.set(true);
  }

  protected async submitInvite(): Promise<void> {
    const email = this.inviteEmail().trim();
    if (!email || !this.inviteRoleId()) {
      this.inviteError.set(this.i18n.translate('ui.emailError'));
      return;
    }
    try {
      await this.store.invite(email, this.inviteRoleId());
      this.inviteOpen.set(false);
      this.activeTab.set('invites');
      this.toast.show(this.i18n.translate('settings.users.inviteSent'), 'success');
    } catch (e) {
      const code = problemDetailsFromError(e as HttpErrorResponse)?.code;
      this.inviteError.set(
        code === 'auth.email_taken'
          ? this.i18n.translate('settings.users.emailTaken')
          : this.i18n.translate('settings.users.inviteFailed'),
      );
    }
  }

  protected openEdit(u: UserListItem): void {
    this.editUser.set(u);
    this.editName.set(u.fullName);
    this.editRoleId.set(u.roleId);
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

  protected async resend(inv: InvitationListItem): Promise<void> {
    try {
      await this.store.resend(inv.id);
      this.toast.show(this.i18n.translate('settings.users.resent'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.users.inviteFailed'), 'danger');
    }
  }

  protected askRevoke(inv: InvitationListItem): void {
    this.revokeTarget.set(inv);
  }

  protected async confirmRevoke(): Promise<void> {
    const inv = this.revokeTarget();
    if (!inv) {
      return;
    }
    try {
      await this.store.revoke(inv.id);
      this.revokeTarget.set(null);
      this.toast.show(this.i18n.translate('settings.users.revoked'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.users.inviteFailed'), 'danger');
    }
  }
}
