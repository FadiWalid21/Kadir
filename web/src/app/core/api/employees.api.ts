import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface EmployeeListItem {
  id: string;
  employeeNumber: string;
  fullNameAr: string;
  fullNameEn: string | null;
  email: string | null;
  phone: string | null;
  nationalId: string | null;
  status: string;
  jobTitleNameAr: string | null;
  jobTitleNameEn: string | null;
  departmentNameAr: string | null;
  departmentNameEn: string | null;
  branchName: string | null;
  managerNameAr: string | null;
  managerId: string | null;
  hireDate: string;
}

export interface EmployeeSummary {
  statusCounts: {
    all: number;
    active: number;
    probation: number;
    onLeave: number;
    suspended: number;
    terminated: number;
  };
  activeForCapacity: number;
  employeeCap: number | null;
}

export interface PagedEmployees {
  items: EmployeeListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  summary: EmployeeSummary;
}

export interface EmergencyContact {
  id?: string;
  name: string;
  relationship: string;
  phone: string;
  sortOrder?: number;
}

export interface EmployeeDocumentItem {
  id: string;
  documentTypeId: string;
  documentTypeNameAr: string;
  documentTypeNameEn: string;
  referenceNumber: string | null;
  issuedOn: string | null;
  expiresOn: string | null;
  expiryStatus: string;
  fileObjectId: string;
  fileName: string | null;
}

export interface EmployeeDetail {
  id: string;
  employeeNumber: string;
  fullNameAr: string;
  fullNameEn: string | null;
  nationalId: string | null;
  nationalIdMasked: string | null;
  gender: string;
  dateOfBirth: string | null;
  email: string | null;
  phone: string | null;
  address: string | null;
  photoFileId: string | null;
  legalEntityId: string;
  branchId: string;
  branchName: string;
  departmentId: string | null;
  departmentNameAr: string | null;
  departmentNameEn: string | null;
  jobTitleId: string | null;
  jobTitleNameAr: string | null;
  jobTitleNameEn: string | null;
  managerEmployeeId: string | null;
  managerNameAr: string | null;
  hireDate: string;
  probationEndDate: string | null;
  contractType: string;
  contractStartDate: string | null;
  contractEndDate: string | null;
  status: string;
  userId: string | null;
  linkedUserEmail: string | null;
  linkedUserFullName: string | null;
  emergencyContacts: EmergencyContact[];
  directReports: EmployeeListItem[];
  urgentDocuments: EmployeeDocumentItem[];
}

export interface CreateEmployeeBody {
  fullNameAr: string;
  fullNameEn?: string | null;
  nationalId?: string | null;
  gender?: string | null;
  dateOfBirth?: string | null;
  email?: string | null;
  phone?: string | null;
  address?: string | null;
  branchId?: string | null;
  departmentId?: string | null;
  jobTitleId?: string | null;
  managerEmployeeId?: string | null;
  hireDate: string;
  probationEndDate?: string | null;
  contractType?: string | null;
  contractStartDate?: string | null;
  contractEndDate?: string | null;
  status?: string | null;
  employeeNumber?: string | null;
  emergencyContacts?: EmergencyContact[] | null;
}

export interface OrgDepartmentNode {
  id: string;
  nameAr: string;
  nameEn: string;
  parentId: string | null;
  managerEmployeeId: string | null;
  managerNameAr: string | null;
  employeeCount: number;
  children: OrgDepartmentNode[];
}

export interface OrgLookupItem {
  id: string;
  nameAr: string;
  nameEn: string;
}

export interface DocumentTypeLookup {
  id: string;
  nameAr: string;
  nameEn: string;
  hasExpiry: boolean;
}

export interface OrgLookups {
  branches: OrgLookupItem[];
  departments: OrgLookupItem[];
  jobTitles: OrgLookupItem[];
  documentTypes: DocumentTypeLookup[];
}

export interface ExpiringDocumentsResult {
  items: {
    documentId: string;
    employeeId: string;
    employeeNameAr: string;
    jobTitleNameAr: string | null;
    departmentNameAr: string | null;
    documentTypeId: string;
    documentTypeNameAr: string;
    documentTypeNameEn: string;
    referenceNumber: string | null;
    expiresOn: string | null;
    expiryStatus: string;
    daysRemaining: number | null;
  }[];
  page: number;
  pageSize: number;
  totalCount: number;
  within60: number;
  within30: number;
  within7: number;
  expired: number;
}

export interface ImportPreviewResult {
  validCount: number;
  errorCount: number;
  errors: { row: number; field: string; code: string; message: string }[];
  validRows: CreateEmployeeBody[];
}

@Injectable({ providedIn: 'root' })
export class EmployeesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/employees`;

  list(opts: {
    page?: number;
    pageSize?: number;
    q?: string;
    status?: string;
    departmentId?: string;
    branchId?: string;
    jobTitleId?: string;
  } = {}): Promise<PagedEmployees> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 25));
    if (opts.q) params = params.set('q', opts.q);
    if (opts.status) params = params.set('status', opts.status);
    if (opts.departmentId) params = params.set('departmentId', opts.departmentId);
    if (opts.branchId) params = params.set('branchId', opts.branchId);
    if (opts.jobTitleId) params = params.set('jobTitleId', opts.jobTitleId);
    return firstValueFrom(this.http.get<PagedEmployees>(this.base, { params }));
  }

  get(id: string): Promise<EmployeeDetail> {
    return firstValueFrom(this.http.get<EmployeeDetail>(`${this.base}/${id}`));
  }

  create(body: CreateEmployeeBody): Promise<EmployeeDetail> {
    return firstValueFrom(this.http.post<EmployeeDetail>(this.base, body));
  }

  update(id: string, body: CreateEmployeeBody & { branchId: string }): Promise<EmployeeDetail> {
    return firstValueFrom(this.http.put<EmployeeDetail>(`${this.base}/${id}`, body));
  }

  changeStatus(id: string, status: string): Promise<EmployeeDetail> {
    return firstValueFrom(
      this.http.post<EmployeeDetail>(`${this.base}/${id}/status`, { status }),
    );
  }

  listDocuments(id: string): Promise<EmployeeDocumentItem[]> {
    return firstValueFrom(this.http.get<EmployeeDocumentItem[]>(`${this.base}/${id}/documents`));
  }

  uploadDocument(
    id: string,
    form: FormData,
  ): Promise<EmployeeDocumentItem> {
    return firstValueFrom(
      this.http.post<EmployeeDocumentItem>(`${this.base}/${id}/documents`, form),
    );
  }

  deleteDocument(employeeId: string, documentId: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.base}/${employeeId}/documents/${documentId}`),
    );
  }

  listExpiring(opts: {
    window?: string;
    q?: string;
    page?: number;
    pageSize?: number;
  } = {}): Promise<ExpiringDocumentsResult> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 25))
      .set('window', opts.window ?? 'all');
    if (opts.q) params = params.set('q', opts.q);
    return firstValueFrom(
      this.http.get<ExpiringDocumentsResult>(`${environment.apiBaseUrl}/documents/expiring`, {
        params,
      }),
    );
  }

  orgTree(): Promise<OrgDepartmentNode[]> {
    return firstValueFrom(this.http.get<OrgDepartmentNode[]>(`${environment.apiBaseUrl}/org/tree`));
  }

  orgLookups(): Promise<OrgLookups> {
    return firstValueFrom(this.http.get<OrgLookups>(`${environment.apiBaseUrl}/org/lookups`));
  }

  updateDepartment(
    id: string,
    body: { parentId: string | null; managerEmployeeId: string | null },
  ): Promise<void> {
    return firstValueFrom(
      this.http.patch<void>(`${environment.apiBaseUrl}/org/departments/${id}`, body),
    );
  }

  downloadDocument(employeeId: string, documentId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${this.base}/${employeeId}/documents/${documentId}/download`, {
        responseType: 'blob',
      }),
    );
  }

  downloadImportTemplate(): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${this.base}/import/template`, { responseType: 'blob' }),
    );
  }

  previewImport(file: File): Promise<ImportPreviewResult> {
    const form = new FormData();
    form.append('file', file);
    return firstValueFrom(
      this.http.post<ImportPreviewResult>(`${this.base}/import/preview`, form),
    );
  }

  commitImport(rows: CreateEmployeeBody[]): Promise<{ importedCount: number; skippedCount: number }> {
    return firstValueFrom(
      this.http.post<{ importedCount: number; skippedCount: number }>(
        `${this.base}/import/commit`,
        rows,
      ),
    );
  }
}
