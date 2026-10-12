import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export type PayrollRunStatus =
  | 'Draft'
  | 'Calculating'
  | 'InReview'
  | 'Approved'
  | 'Locked'
  | 'Superseded'
  | string;

export interface PayrollRunListItem {
  id: string;
  legalEntityId: string;
  periodStart: string;
  periodEnd: string;
  status: PayrollRunStatus;
  warningCount: number;
  headcount: number;
  totalGross: number | null;
  totalNet: number | null;
  totalEmployerCost: number | null;
  createdBy: string;
  approvedBy: string | null;
  lockedAt: string | null;
  reopenedFromRunId: string | null;
  version: number;
  createdAt: string;
}

export interface PagedPayrollRuns {
  items: PayrollRunListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface PayrollRunDetail {
  id: string;
  legalEntityId: string;
  periodStart: string;
  periodEnd: string;
  status: PayrollRunStatus;
  rulesSnapshotJson: string;
  warningCount: number;
  headcount: number;
  totalGross: number | null;
  totalNet: number | null;
  totalEmployerCost: number | null;
  createdBy: string;
  approvedBy: string | null;
  lockedAt: string | null;
  reopenedFromRunId: string | null;
  version: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface PayrollEntryListItem {
  id: string;
  runId: string;
  employeeId: string;
  employeeNumber: string;
  fullNameAr: string;
  fullNameEn: string | null;
  departmentId: string | null;
  gross: number;
  net: number;
  employerCostTotal: number;
  warningCount: number;
  isHeld: boolean;
  holdReason: string | null;
}

export interface PagedPayrollEntries {
  items: PayrollEntryListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface PayrollEntryLine {
  id: string;
  lineType: string;
  componentKey: string | null;
  labelAr: string;
  labelEn: string;
  amount: number;
  calcExplanationJson: string | null;
  sortOrder: number;
}

export interface PayrollEntryDetail {
  id: string;
  runId: string;
  employeeId: string;
  employeeNumber: string;
  fullNameAr: string;
  fullNameEn: string | null;
  salarySnapshotJson: string;
  inputsJson: string;
  gross: number;
  insurableWage: number;
  employeeInsurance: number;
  employerInsurance: number;
  taxableIncome: number;
  incomeTax: number;
  otherDeductions: number;
  net: number;
  employerCostTotal: number;
  isHeld: boolean;
  holdReason: string | null;
  warningCount: number;
  warningsJson: string | null;
  lines: PayrollEntryLine[];
}

export interface CreatePayrollRunBody {
  periodStart: string;
  periodEnd: string;
}

export interface ReopenPayrollRunBody {
  reason: string;
}

export interface HoldPayrollEntryBody {
  reason?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PayrollRunsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/payroll/runs`;

  list(opts: { page?: number; pageSize?: number } = {}): Promise<PagedPayrollRuns> {
    const params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 50));
    return firstValueFrom(this.http.get<PagedPayrollRuns>(this.base, { params }));
  }

  create(body: CreatePayrollRunBody): Promise<PayrollRunDetail> {
    return firstValueFrom(this.http.post<PayrollRunDetail>(this.base, body));
  }

  get(id: string): Promise<PayrollRunDetail> {
    return firstValueFrom(this.http.get<PayrollRunDetail>(`${this.base}/${id}`));
  }

  calculate(id: string): Promise<PayrollRunDetail> {
    return firstValueFrom(this.http.post<PayrollRunDetail>(`${this.base}/${id}/calculate`, {}));
  }

  submit(id: string): Promise<PayrollRunDetail> {
    return firstValueFrom(this.http.post<PayrollRunDetail>(`${this.base}/${id}/submit`, {}));
  }

  approve(id: string): Promise<PayrollRunDetail> {
    return firstValueFrom(this.http.post<PayrollRunDetail>(`${this.base}/${id}/approve`, {}));
  }

  lock(id: string): Promise<PayrollRunDetail> {
    return firstValueFrom(this.http.post<PayrollRunDetail>(`${this.base}/${id}/lock`, {}));
  }

  reopen(id: string, body: ReopenPayrollRunBody): Promise<PayrollRunDetail> {
    return firstValueFrom(this.http.post<PayrollRunDetail>(`${this.base}/${id}/reopen`, body));
  }

  listEntries(
    runId: string,
    opts: {
      page?: number;
      pageSize?: number;
      q?: string;
      departmentId?: string;
      warnings?: boolean;
      held?: boolean;
    } = {},
  ): Promise<PagedPayrollEntries> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 50));
    if (opts.q) {
      params = params.set('q', opts.q);
    }
    if (opts.departmentId) {
      params = params.set('departmentId', opts.departmentId);
    }
    if (opts.warnings === true || opts.warnings === false) {
      params = params.set('warnings', String(opts.warnings));
    }
    if (opts.held === true || opts.held === false) {
      params = params.set('held', String(opts.held));
    }
    return firstValueFrom(
      this.http.get<PagedPayrollEntries>(`${this.base}/${runId}/entries`, { params }),
    );
  }

  getEntry(entryId: string): Promise<PayrollEntryDetail> {
    return firstValueFrom(
      this.http.get<PayrollEntryDetail>(`${this.base}/entries/${entryId}`),
    );
  }

  holdEntry(entryId: string, body: HoldPayrollEntryBody = {}): Promise<PayrollEntryDetail> {
    return firstValueFrom(
      this.http.post<PayrollEntryDetail>(`${this.base}/entries/${entryId}/hold`, body),
    );
  }
}
