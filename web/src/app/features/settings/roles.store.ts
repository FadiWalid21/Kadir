import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  PermissionGroup,
  RoleDetail,
  RoleListItem,
  RolesApi,
  UpsertRoleBody,
} from '../../core/api/roles.api';

export type RolesLoadState = 'idle' | 'loading' | 'ready' | 'error';

@Injectable()
export class RolesStore {
  private readonly api = inject(RolesApi);

  readonly listState = signal<RolesLoadState>('idle');
  readonly detailState = signal<RolesLoadState>('idle');
  readonly roles = signal<RoleListItem[]>([]);
  readonly selected = signal<RoleDetail | null>(null);
  readonly permissionGroups = signal<PermissionGroup[]>([]);
  readonly busy = signal(false);
  readonly errorCode = signal<string | null>(null);

  async loadAll(): Promise<void> {
    this.listState.set('loading');
    this.errorCode.set(null);
    try {
      const [roles, groups] = await Promise.all([this.api.list(), this.api.listPermissions()]);
      this.roles.set(roles);
      this.permissionGroups.set(groups);
      this.listState.set('ready');
      if (roles.length > 0 && !this.selected()) {
        await this.selectRole(roles[0].id);
      }
    } catch (e) {
      this.listState.set('error');
      this.errorCode.set(problemDetailsFromError(e as HttpErrorResponse)?.code ?? 'error');
    }
  }

  async selectRole(id: string): Promise<void> {
    this.detailState.set('loading');
    try {
      this.selected.set(await this.api.get(id));
      this.detailState.set('ready');
    } catch {
      this.detailState.set('error');
      this.selected.set(null);
    }
  }

  async create(body: UpsertRoleBody): Promise<RoleDetail> {
    this.busy.set(true);
    try {
      const created = await this.api.create(body);
      await this.loadAll();
      await this.selectRole(created.id);
      return created;
    } finally {
      this.busy.set(false);
    }
  }

  async update(id: string, body: UpsertRoleBody): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.update(id, body);
      await this.loadAll();
      await this.selectRole(id);
    } finally {
      this.busy.set(false);
    }
  }

  async delete(id: string): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.delete(id);
      this.selected.set(null);
      await this.loadAll();
    } finally {
      this.busy.set(false);
    }
  }
}
