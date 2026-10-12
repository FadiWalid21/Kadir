import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ReportHubItem {
  key: string;
  titleKey: string;
  groupKey: string;
  route: string;
  available: boolean;
}

export interface HeadcountStatusCount {
  status: string;
  count: number;
}

export interface HeadcountSummary {
  total: number;
  byStatus: HeadcountStatusCount[];
}

export interface HeadcountRow {
  employeeId: string;
  employeeNumber: string;
  fullNameAr: string;
  fullNameEn: string | null;
  branchNameAr: string;
  branchNameEn: string | null;
  departmentNameAr: string | null;
  departmentNameEn: string | null;
  status: string;
  contractType: string;
  hireDate: string;
}

export interface HeadcountReport {
  asOf: string;
  summary: HeadcountSummary;
  items: HeadcountRow[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface HeadcountQuery {
  asOf?: string;
  branchId?: string;
  departmentId?: string;
  status?: string[];
  contractType?: string[];
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDesc?: boolean;
}

@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/reports`;

  listHub(): Promise<ReportHubItem[]> {
    return firstValueFrom(this.http.get<ReportHubItem[]>(this.base));
  }

  headcount(query: HeadcountQuery = {}): Promise<HeadcountReport> {
    return firstValueFrom(this.http.get<HeadcountReport>(`${this.base}/headcount`, { params: this.toParams(query) }));
  }

  exportHeadcount(format: 'xlsx' | 'pdf', query: HeadcountQuery = {}): Promise<Blob> {
    const params = this.toParams(query).set('format', format);
    return firstValueFrom(
      this.http.get(`${this.base}/headcount/export`, {
        params,
        responseType: 'blob',
      }),
    );
  }

  private toParams(query: HeadcountQuery): HttpParams {
    let params = new HttpParams()
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));
    if (query.asOf) {
      params = params.set('asOf', query.asOf);
    }
    if (query.branchId) {
      params = params.set('branchId', query.branchId);
    }
    if (query.departmentId) {
      params = params.set('departmentId', query.departmentId);
    }
    if (query.sortBy) {
      params = params.set('sortBy', query.sortBy);
    }
    if (query.sortDesc) {
      params = params.set('sortDesc', 'true');
    }
    for (const s of query.status ?? []) {
      params = params.append('status', s);
    }
    for (const c of query.contractType ?? []) {
      params = params.append('contractType', c);
    }
    return params;
  }
}
