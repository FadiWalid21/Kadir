import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export type DataScope = 'Own' | 'Team' | 'Department' | 'Branch' | 'All' | number;

export interface UserListItem {
  id: string;
  fullName: string;
  email: string;
  roleId: string;
  roleName: string;
  isSystemRole: boolean;
  isActive: boolean;
  dataScope: DataScope;
  lastLoginAt: string | null;
}

export interface UserDetail extends UserListItem {
  createdAt: string;
}

export interface PagedUsers {
  items: UserListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface UpdateUserBody {
  fullName: string;
  roleId: string;
  isActive: boolean;
  dataScope: DataScope;
}

export interface InvitationListItem {
  id: string;
  email: string;
  roleId: string;
  roleName: string;
  expiresAt: string;
  createdAt: string;
  /** Present once on invite/resend responses — never stored for list rows. */
  acceptUrl?: string | null;
}

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/users`;
  private readonly invitationsBase = `${environment.apiBaseUrl}/invitations`;

  list(opts: {
    page?: number;
    pageSize?: number;
    q?: string;
    roleId?: string;
    active?: boolean | null;
  } = {}): Promise<PagedUsers> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 25));
    if (opts.q) {
      params = params.set('q', opts.q);
    }
    if (opts.roleId) {
      params = params.set('roleId', opts.roleId);
    }
    if (opts.active === true || opts.active === false) {
      params = params.set('active', String(opts.active));
    }
    return firstValueFrom(this.http.get<PagedUsers>(this.base, { params }));
  }

  get(id: string): Promise<UserDetail> {
    return firstValueFrom(this.http.get<UserDetail>(`${this.base}/${id}`));
  }

  update(id: string, body: UpdateUserBody): Promise<UserDetail> {
    return firstValueFrom(this.http.put<UserDetail>(`${this.base}/${id}`, body));
  }

  invite(email: string, roleId: string): Promise<InvitationListItem> {
    return firstValueFrom(
      this.http.post<InvitationListItem>(`${this.base}/invite`, { email, roleId }),
    );
  }

  listPendingInvitations(): Promise<InvitationListItem[]> {
    return firstValueFrom(this.http.get<InvitationListItem[]>(this.invitationsBase));
  }

  resendInvitation(id: string, roleId?: string): Promise<InvitationListItem> {
    return firstValueFrom(
      this.http.post<InvitationListItem>(`${this.invitationsBase}/${id}/resend`, {
        roleId: roleId ?? null,
      }),
    );
  }

  revokeInvitation(id: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.invitationsBase}/${id}/revoke`, {}));
  }
}
