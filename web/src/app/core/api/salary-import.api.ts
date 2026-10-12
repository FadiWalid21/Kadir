import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface SalaryImportPreviewError {
  row: number;
  field: string;
  code: string;
  message: string;
}

export interface SalaryImportCommitRow {
  employeeId: string;
  employeeNumber: string;
  effectiveFrom: string;
  currency: string;
  basicAmount: number;
  changeReason: string;
  components: { payComponentId: string; amountOrRate: number }[];
}

export interface SalaryImportPreviewResult {
  validCount: number;
  errorCount: number;
  errors: SalaryImportPreviewError[];
  validRows: SalaryImportCommitRow[];
}

export interface SalaryImportCommitResult {
  importedCount: number;
  skippedCount: number;
  errors: SalaryImportPreviewError[];
}

@Injectable({ providedIn: 'root' })
export class SalaryImportApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/payroll/salaries/import`;

  downloadTemplate(): Promise<Blob> {
    return firstValueFrom(this.http.get(`${this.base}/template`, { responseType: 'blob' }));
  }

  preview(file: File): Promise<SalaryImportPreviewResult> {
    const form = new FormData();
    form.append('file', file);
    return firstValueFrom(this.http.post<SalaryImportPreviewResult>(`${this.base}/preview`, form));
  }

  commit(rows: SalaryImportCommitRow[]): Promise<SalaryImportCommitResult> {
    return firstValueFrom(this.http.post<SalaryImportCommitResult>(`${this.base}/commit`, rows));
  }
}
