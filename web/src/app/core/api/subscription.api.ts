import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PlanCatalogItemDto, SubscriptionDto } from './subscription.models';

@Injectable({ providedIn: 'root' })
export class SubscriptionApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  getSubscription(): Promise<SubscriptionDto> {
    return firstValueFrom(this.http.get<SubscriptionDto>(`${this.base}/subscription`));
  }

  listPlans(): Promise<PlanCatalogItemDto[]> {
    return firstValueFrom(this.http.get<PlanCatalogItemDto[]>(`${this.base}/plans`));
  }
}
