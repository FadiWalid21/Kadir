import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PlanCatalogItemDto } from './subscription.models';

@Injectable({ providedIn: 'root' })
export class PublicPlansApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/public/plans`;

  list(): Promise<PlanCatalogItemDto[]> {
    return firstValueFrom(this.http.get<PlanCatalogItemDto[]>(this.base));
  }
}
