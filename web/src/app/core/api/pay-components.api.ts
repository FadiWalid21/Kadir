import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export type PayComponentKind = 'Earning' | 'Deduction' | 'EmployerCost';
export type PayComponentCalcType = 'Fixed' | 'PercentOfBasic' | 'PercentOfGross';

export interface PayComponent {
  id: string;
  key: string;
  nameAr: string;
  nameEn: string;
  kind: PayComponentKind | string;
  calcType: PayComponentCalcType | string;
  isTaxable: boolean;
  isInsurable: boolean;
  isActive: boolean;
  sourceKey: string | null;
}

export interface PagedPayComponents {
  items: PayComponent[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreatePayComponentBody {
  key: string;
  nameAr: string;
  nameEn: string;
  kind: string;
  calcType: string;
  isTaxable: boolean;
  isInsurable: boolean;
}

export interface UpdatePayComponentBody {
  nameAr: string;
  nameEn: string;
  kind: string;
  calcType: string;
  isTaxable: boolean;
  isInsurable: boolean;
}

@Injectable({ providedIn: 'root' })
export class PayComponentsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/payroll/pay-components`;

  list(opts: {
    page?: number;
    pageSize?: number;
    q?: string;
    isActive?: boolean | null;
  } = {}): Promise<PagedPayComponents> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 50));
    if (opts.q) {
      params = params.set('q', opts.q);
    }
    if (opts.isActive === true || opts.isActive === false) {
      params = params.set('isActive', String(opts.isActive));
    }
    return firstValueFrom(this.http.get<PagedPayComponents>(this.base, { params }));
  }

  get(id: string): Promise<PayComponent> {
    return firstValueFrom(this.http.get<PayComponent>(`${this.base}/${id}`));
  }

  create(body: CreatePayComponentBody): Promise<PayComponent> {
    return firstValueFrom(this.http.post<PayComponent>(this.base, body));
  }

  update(id: string, body: UpdatePayComponentBody): Promise<PayComponent> {
    return firstValueFrom(this.http.put<PayComponent>(`${this.base}/${id}`, body));
  }

  deactivate(id: string): Promise<PayComponent> {
    return firstValueFrom(this.http.post<PayComponent>(`${this.base}/${id}/deactivate`, {}));
  }
}
