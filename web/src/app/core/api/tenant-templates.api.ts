import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export type TemplateSection =
  | 'shifts'
  | 'leave-types'
  | 'document-types'
  | 'job-titles'
  | 'departments';

export interface TenantTemplatesOverview {
  industryCode: string;
  industryNameAr: string;
  industryNameEn: string;
  appliedVersion: number;
  latestVersion: number;
  updateAvailable: boolean;
  missingKeys: string[];
}

export interface TemplateItem {
  id: string;
  section: string;
  sourceKey: string | null;
  sourceVersion: number;
  isCustomized: boolean;
  isActive: boolean;
  nameAr: string;
  nameEn: string;
  fields: Record<string, unknown>;
}

export interface PagedTemplateItems {
  items: TemplateItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface AddMissingResult {
  addedCount: number;
  addedKeys: string[];
}

@Injectable({ providedIn: 'root' })
export class TenantTemplatesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/tenant/templates`;

  getOverview(): Promise<TenantTemplatesOverview> {
    return firstValueFrom(this.http.get<TenantTemplatesOverview>(this.base));
  }

  listSection(
    section: TemplateSection,
    opts: { page?: number; pageSize?: number; q?: string; active?: boolean | null } = {},
  ): Promise<PagedTemplateItems> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 50));
    if (opts.q) {
      params = params.set('q', opts.q);
    }
    if (opts.active === true || opts.active === false) {
      params = params.set('active', String(opts.active));
    }
    return firstValueFrom(
      this.http.get<PagedTemplateItems>(`${this.base}/${section}`, { params }),
    );
  }

  updateItem(section: TemplateSection, id: string, body: unknown): Promise<TemplateItem> {
    return firstValueFrom(this.http.put<TemplateItem>(`${this.base}/${section}/${id}`, body));
  }

  resetItem(section: TemplateSection, id: string): Promise<TemplateItem> {
    return firstValueFrom(this.http.post<TemplateItem>(`${this.base}/${section}/${id}/reset`, {}));
  }

  setActive(section: TemplateSection, id: string, isActive: boolean): Promise<TemplateItem> {
    return firstValueFrom(
      this.http.patch<TemplateItem>(`${this.base}/${section}/${id}/active`, { isActive }),
    );
  }

  addMissing(): Promise<AddMissingResult> {
    return firstValueFrom(this.http.post<AddMissingResult>(`${this.base}/add-missing`, {}));
  }
}
