import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface BranchListItem {
  id: string;
  legalEntityId: string;
  legalEntityName: string;
  nameAr: string;
  nameEn: string;
  address: string | null;
  isDefault: boolean;
  isActive: boolean;
}

export interface PagedBranches {
  items: BranchListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface BranchWriteBody {
  legalEntityId: string;
  nameAr: string;
  nameEn: string;
  address?: string | null;
}

@Injectable({ providedIn: 'root' })
export class BranchSettingsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/settings/branches`;

  list(opts: {
    page?: number;
    pageSize?: number;
    q?: string;
    active?: boolean | null;
  } = {}): Promise<PagedBranches> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 50));
    if (opts.q) {
      params = params.set('q', opts.q);
    }
    if (opts.active === true || opts.active === false) {
      params = params.set('active', String(opts.active));
    }
    return firstValueFrom(this.http.get<PagedBranches>(this.base, { params }));
  }

  create(body: BranchWriteBody): Promise<BranchListItem> {
    return firstValueFrom(this.http.post<BranchListItem>(this.base, body));
  }

  update(id: string, body: BranchWriteBody): Promise<BranchListItem> {
    return firstValueFrom(this.http.put<BranchListItem>(`${this.base}/${id}`, body));
  }

  setActive(id: string, isActive: boolean): Promise<BranchListItem> {
    return firstValueFrom(
      this.http.patch<BranchListItem>(`${this.base}/${id}/active`, { isActive }),
    );
  }

  delete(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/${id}`));
  }
}
