import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface EmployeeInsuranceProfile {
  id: string;
  employeeId: string;
  isInsured: boolean;
  insuranceNumber: string | null;
  insuranceStartDate: string | null;
  insurableWageOverride: number | null;
}

export interface UpsertEmployeeInsuranceBody {
  isInsured: boolean;
  insuranceNumber?: string | null;
  insuranceStartDate?: string | null;
  insurableWageOverride?: number | null;
}

@Injectable({ providedIn: 'root' })
export class EmployeeInsuranceApi {
  private readonly http = inject(HttpClient);
  private readonly root = `${environment.apiBaseUrl}/employees`;

  get(employeeId: string): Promise<EmployeeInsuranceProfile | null> {
    return firstValueFrom(
      this.http.get<EmployeeInsuranceProfile | null>(`${this.root}/${employeeId}/insurance`),
    );
  }

  upsert(employeeId: string, body: UpsertEmployeeInsuranceBody): Promise<EmployeeInsuranceProfile> {
    return firstValueFrom(
      this.http.put<EmployeeInsuranceProfile>(`${this.root}/${employeeId}/insurance`, body),
    );
  }
}
