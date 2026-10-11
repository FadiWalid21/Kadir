import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditLogListQuery, AuditLogListResult } from './audit.models';

@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/audit-logs`;

  list(query: AuditLogListQuery = {}): Promise<AuditLogListResult> {
    let params = new HttpParams().set('pageSize', String(query.pageSize ?? 25));
    if (query.userId) {
      params = params.set('userId', query.userId);
    }
    if (query.entityType) {
      params = params.set('entityType', query.entityType);
    }
    if (query.action) {
      params = params.set('action', query.action);
    }
    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    if (query.cursor) {
      params = params.set('cursor', query.cursor);
    }
    return firstValueFrom(this.http.get<AuditLogListResult>(this.base, { params }));
  }

  exportCsv(query: Omit<AuditLogListQuery, 'pageSize' | 'cursor'> = {}): Promise<Blob> {
    let params = new HttpParams();
    if (query.userId) {
      params = params.set('userId', query.userId);
    }
    if (query.entityType) {
      params = params.set('entityType', query.entityType);
    }
    if (query.action) {
      params = params.set('action', query.action);
    }
    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    return firstValueFrom(
      this.http.get(`${this.base}/export`, { params, responseType: 'blob' }),
    );
  }
}
