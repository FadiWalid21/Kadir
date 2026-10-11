import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { RoleListItem, RolesApi } from '../../core/api/roles.api';
import {
  InvitationListItem,
  PagedUsers,
  UpdateUserBody,
  UsersApi,
} from '../../core/api/users.api';

export type UsersLoadState = 'idle' | 'loading' | 'ready' | 'error';

@Injectable()
export class UsersStore {
  private readonly usersApi = inject(UsersApi);
  private readonly rolesApi = inject(RolesApi);

  readonly listState = signal<UsersLoadState>('idle');
  readonly invitesState = signal<UsersLoadState>('idle');
  readonly users = signal<PagedUsers | null>(null);
  readonly invitations = signal<InvitationListItem[]>([]);
  readonly roles = signal<RoleListItem[]>([]);
  readonly busy = signal(false);
  readonly errorCode = signal<string | null>(null);

  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly q = signal('');
  readonly roleFilter = signal('');
  readonly activeFilter = signal<'all' | 'true' | 'false'>('all');

  async loadRoles(): Promise<void> {
    try {
      this.roles.set(await this.rolesApi.list());
    } catch {
      this.roles.set([]);
    }
  }

  async loadUsers(): Promise<void> {
    this.listState.set('loading');
    this.errorCode.set(null);
    try {
      const active =
        this.activeFilter() === 'all' ? null : this.activeFilter() === 'true';
      const data = await this.usersApi.list({
        page: this.page(),
        pageSize: this.pageSize(),
        q: this.q() || undefined,
        roleId: this.roleFilter() || undefined,
        active,
      });
      this.users.set(data);
      this.listState.set('ready');
    } catch (e) {
      this.listState.set('error');
      this.errorCode.set(problemDetailsFromError(e as HttpErrorResponse)?.code ?? 'error');
    }
  }

  async loadInvitations(): Promise<void> {
    this.invitesState.set('loading');
    try {
      this.invitations.set(await this.usersApi.listPendingInvitations());
      this.invitesState.set('ready');
    } catch {
      this.invitesState.set('error');
    }
  }

  async invite(email: string, roleId: string): Promise<void> {
    this.busy.set(true);
    try {
      await this.usersApi.invite(email, roleId);
      await this.loadInvitations();
    } finally {
      this.busy.set(false);
    }
  }

  async resend(id: string, roleId?: string): Promise<void> {
    this.busy.set(true);
    try {
      await this.usersApi.resendInvitation(id, roleId);
      await this.loadInvitations();
    } finally {
      this.busy.set(false);
    }
  }

  async revoke(id: string): Promise<void> {
    this.busy.set(true);
    try {
      await this.usersApi.revokeInvitation(id);
      await this.loadInvitations();
    } finally {
      this.busy.set(false);
    }
  }

  async updateUser(id: string, body: UpdateUserBody): Promise<void> {
    this.busy.set(true);
    try {
      await this.usersApi.update(id, body);
      await this.loadUsers();
    } finally {
      this.busy.set(false);
    }
  }
}
