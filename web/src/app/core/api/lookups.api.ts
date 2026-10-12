import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LookupItem {
  id: string;
  name: string;
}

export interface PagedLookup {
  items: LookupItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class LookupsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/lookups`;
  private readonly platformBase = `${environment.apiBaseUrl}/platform/lookups`;

  searchEmployees(q: string, page = 1, pageSize = 20): Promise<PagedLookup> {
    return this.get(`${this.base}/employees`, q, page, pageSize);
  }

  searchDepartments(q: string, page = 1, pageSize = 20): Promise<PagedLookup> {
    return this.get(`${this.base}/departments`, q, page, pageSize);
  }

  searchBranches(q: string, page = 1, pageSize = 20): Promise<PagedLookup> {
    return this.get(`${this.base}/branches`, q, page, pageSize);
  }

  searchJobTitles(q: string, page = 1, pageSize = 20): Promise<PagedLookup> {
    return this.get(`${this.base}/job-titles`, q, page, pageSize);
  }

  searchLegalEntities(q: string, page = 1, pageSize = 20): Promise<PagedLookup> {
    return this.get(`${this.base}/legal-entities`, q, page, pageSize);
  }

  searchTenants(q: string, page = 1, pageSize = 20): Promise<PagedLookup> {
    return this.get(`${this.platformBase}/tenants`, q, page, pageSize);
  }

  private get(url: string, q: string, page: number, pageSize: number): Promise<PagedLookup> {
    let params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    if (q.trim()) {
      params = params.set('q', q.trim());
    }
    return firstValueFrom(this.http.get<PagedLookup>(url, { params }));
  }
}
