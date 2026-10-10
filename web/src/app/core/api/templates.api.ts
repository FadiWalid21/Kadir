import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface IndustryDto {
  code: string;
  nameAr: string;
  nameEn: string;
}

@Injectable({ providedIn: 'root' })
export class TemplatesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/templates`;

  listIndustries(): Promise<IndustryDto[]> {
    return firstValueFrom(this.http.get<IndustryDto[]>(`${this.base}/industries`));
  }
}
