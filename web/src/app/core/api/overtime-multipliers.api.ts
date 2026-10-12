import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface OvertimeMultipliers {
  day: number;
  night: number;
  holiday: number;
}

@Injectable({ providedIn: 'root' })
export class OvertimeMultipliersApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/payroll/overtime-multipliers`;

  get(): Promise<OvertimeMultipliers> {
    return firstValueFrom(this.http.get<OvertimeMultipliers>(this.base));
  }
}
