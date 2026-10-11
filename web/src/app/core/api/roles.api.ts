import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface RoleListItem {
  id: string;
  name: string;
  isSystem: boolean;
  descriptionAr: string | null;
  descriptionEn: string | null;
  permissionCount: number;
  userCount: number;
}

export interface RoleDetail {
  id: string;
  name: string;
  isSystem: boolean;
  descriptionAr: string | null;
  descriptionEn: string | null;
  permissions: string[];
  userCount: number;
}

export interface PermissionItem {
  code: string;
  labelKey: string;
}

export interface PermissionGroup {
  resource: string;
  permissions: PermissionItem[];
}

export interface UpsertRoleBody {
  name: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  permissions: string[];
}

@Injectable({ providedIn: 'root' })
export class RolesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/roles`;
  private readonly permissionsBase = `${environment.apiBaseUrl}/permissions`;

  list(): Promise<RoleListItem[]> {
    return firstValueFrom(this.http.get<RoleListItem[]>(this.base));
  }

  get(id: string): Promise<RoleDetail> {
    return firstValueFrom(this.http.get<RoleDetail>(`${this.base}/${id}`));
  }

  create(body: UpsertRoleBody): Promise<RoleDetail> {
    return firstValueFrom(this.http.post<RoleDetail>(this.base, body));
  }

  update(id: string, body: UpsertRoleBody): Promise<RoleDetail> {
    return firstValueFrom(this.http.put<RoleDetail>(`${this.base}/${id}`, body));
  }

  delete(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/${id}`));
  }

  listPermissions(): Promise<PermissionGroup[]> {
    return firstValueFrom(this.http.get<PermissionGroup[]>(this.permissionsBase));
  }
}
