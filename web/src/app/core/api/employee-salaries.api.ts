import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface EmployeeSalaryComponentLine {
  id: string;
  payComponentId: string;
  payComponentKey: string;
  payComponentNameAr: string;
  payComponentNameEn: string;
  amountOrRate: number;
}

export interface EmployeeSalary {
  id: string;
  employeeId: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  currency: string;
  basicAmount: number;
  changeReason: string;
  components: EmployeeSalaryComponentLine[];
}

export interface EmployeeSalaryListItem {
  id: string;
  employeeId: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  currency: string;
  basicAmount: number;
  changeReason: string;
  componentCount: number;
}

export interface PagedEmployeeSalaries {
  items: EmployeeSalaryListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateEmployeeSalaryBody {
  effectiveFrom: string;
  currency?: string | null;
  basicAmount: number;
  changeReason: string;
  components?: { payComponentId: string; amountOrRate: number }[] | null;
}

@Injectable({ providedIn: 'root' })
export class EmployeeSalariesApi {
  private readonly http = inject(HttpClient);
  private readonly root = `${environment.apiBaseUrl}/employees`;

  list(employeeId: string, page = 1, pageSize = 50): Promise<PagedEmployeeSalaries> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return firstValueFrom(
      this.http.get<PagedEmployeeSalaries>(`${this.root}/${employeeId}/salaries`, { params }),
    );
  }

  getCurrent(employeeId: string): Promise<EmployeeSalary | null> {
    return firstValueFrom(
      this.http.get<EmployeeSalary | null>(`${this.root}/${employeeId}/salaries/current`),
    );
  }

  get(employeeId: string, salaryId: string): Promise<EmployeeSalary> {
    return firstValueFrom(
      this.http.get<EmployeeSalary>(`${this.root}/${employeeId}/salaries/${salaryId}`),
    );
  }

  create(employeeId: string, body: CreateEmployeeSalaryBody): Promise<EmployeeSalary> {
    return firstValueFrom(
      this.http.post<EmployeeSalary>(`${this.root}/${employeeId}/salaries`, body),
    );
  }
}
