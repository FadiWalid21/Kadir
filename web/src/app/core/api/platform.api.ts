import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthHttpConstants } from './auth.constants';
import {
  PlatformAccessTokenResponse,
  PlatformAuditLogListResult,
  PlatformHealth,
  PlatformMeResponse,
  PlatformPlan,
  PlatformTenantAuditSummary,
  PlatformTenantDetail,
  PlatformTenantListResult,
  PlatformUpdatePlanRequest,
} from './platform.models';

@Injectable({ providedIn: 'root' })
export class PlatformApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/platform`;

  login(email: string, password: string): Promise<PlatformAccessTokenResponse> {
    return firstValueFrom(
      this.http.post<PlatformAccessTokenResponse>(
        `${this.base}/auth/login`,
        { email, password },
        { withCredentials: true },
      ),
    );
  }

  refresh(): Promise<PlatformAccessTokenResponse> {
    return firstValueFrom(
      this.http.post<PlatformAccessTokenResponse>(`${this.base}/auth/refresh`, {}, {
        withCredentials: true,
        headers: {
          [AuthHttpConstants.csrfHeaderName]: AuthHttpConstants.csrfHeaderValue,
        },
      }),
    );
  }

  logout(): Promise<void> {
    return firstValueFrom(
      this.http.post(
        `${this.base}/auth/logout`,
        {},
        {
          withCredentials: true,
          headers: {
            [AuthHttpConstants.csrfHeaderName]: AuthHttpConstants.csrfHeaderValue,
          },
          observe: 'response',
          responseType: 'text',
        },
      ),
    ).then(() => undefined);
  }

  me(): Promise<PlatformMeResponse> {
    return firstValueFrom(this.http.get<PlatformMeResponse>(`${this.base}/me`));
  }

  listTenants(opts: {
    q?: string;
    status?: string;
    plan?: string;
    industry?: string;
    page?: number;
    pageSize?: number;
  }): Promise<PlatformTenantListResult> {
    let params = new HttpParams();
    if (opts.q) params = params.set('q', opts.q);
    if (opts.status) params = params.set('status', opts.status);
    if (opts.plan) params = params.set('plan', opts.plan);
    if (opts.industry) params = params.set('industry', opts.industry);
    if (opts.page) params = params.set('page', String(opts.page));
    if (opts.pageSize) params = params.set('pageSize', String(opts.pageSize));
    return firstValueFrom(
      this.http.get<PlatformTenantListResult>(`${this.base}/tenants`, { params }),
    );
  }

  getTenant(id: string): Promise<PlatformTenantDetail> {
    return firstValueFrom(this.http.get<PlatformTenantDetail>(`${this.base}/tenants/${id}`));
  }

  auditSummary(id: string): Promise<PlatformTenantAuditSummary> {
    return firstValueFrom(
      this.http.get<PlatformTenantAuditSummary>(`${this.base}/tenants/${id}/audit-summary`),
    );
  }

  changePlan(id: string, planCode: string, reason: string): Promise<PlatformTenantDetail> {
    return firstValueFrom(
      this.http.post<PlatformTenantDetail>(`${this.base}/tenants/${id}/change-plan`, {
        planCode,
        reason,
      }),
    );
  }

  extendTrial(id: string, days: number, reason: string): Promise<PlatformTenantDetail> {
    return firstValueFrom(
      this.http.post<PlatformTenantDetail>(`${this.base}/tenants/${id}/extend-trial`, {
        days,
        reason,
      }),
    );
  }

  suspend(id: string, reason: string): Promise<PlatformTenantDetail> {
    return firstValueFrom(
      this.http.post<PlatformTenantDetail>(`${this.base}/tenants/${id}/suspend`, { reason }),
    );
  }

  resume(id: string, reason: string): Promise<PlatformTenantDetail> {
    return firstValueFrom(
      this.http.post<PlatformTenantDetail>(`${this.base}/tenants/${id}/resume`, { reason }),
    );
  }

  cancelSubscription(id: string, reason: string): Promise<PlatformTenantDetail> {
    return firstValueFrom(
      this.http.post<PlatformTenantDetail>(`${this.base}/tenants/${id}/cancel-subscription`, {
        reason,
      }),
    );
  }

  changeIndustry(
    id: string,
    industryCode: string,
    mode: string,
    reason: string,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.post(`${this.base}/tenants/${id}/change-industry`, {
        industryCode,
        mode,
        reason,
      }),
    );
  }

  listPlans(): Promise<{ items: PlatformPlan[] }> {
    return firstValueFrom(this.http.get<{ items: PlatformPlan[] }>(`${this.base}/plans`));
  }

  updatePlan(id: string, body: PlatformUpdatePlanRequest): Promise<PlatformPlan> {
    return firstValueFrom(this.http.put<PlatformPlan>(`${this.base}/plans/${id}`, body));
  }

  listAuditLogs(opts: {
    tenantId?: string;
    entityType?: string;
    action?: string;
    from?: string;
    to?: string;
    pageSize?: number;
    cursor?: string;
  }): Promise<PlatformAuditLogListResult> {
    let params = new HttpParams();
    if (opts.tenantId) params = params.set('tenantId', opts.tenantId);
    if (opts.entityType) params = params.set('entityType', opts.entityType);
    if (opts.action) params = params.set('action', opts.action);
    if (opts.from) params = params.set('from', opts.from);
    if (opts.to) params = params.set('to', opts.to);
    if (opts.pageSize) params = params.set('pageSize', String(opts.pageSize));
    if (opts.cursor) params = params.set('cursor', opts.cursor);
    return firstValueFrom(
      this.http.get<PlatformAuditLogListResult>(`${this.base}/audit-logs`, { params }),
    );
  }

  health(): Promise<PlatformHealth> {
    return firstValueFrom(this.http.get<PlatformHealth>(`${this.base}/health`));
  }
}
