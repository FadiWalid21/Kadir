import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LeaveType {
  id: string;
  nameAr: string;
  nameEn: string;
  paid: boolean;
  daysPerYear: number | null;
  requiresApproval: boolean;
  requiresAttachment: boolean;
  isActive: boolean;
  sourceKey: string | null;
  isCustomized: boolean;
}

export interface LeaveBalance {
  employeeId: string;
  employeeNameAr: string;
  employeeNameEn: string;
  leaveTypeId: string;
  leaveTypeNameAr: string;
  leaveTypeNameEn: string;
  year: number;
  entitled: number;
  accrued: number;
  used: number;
  adjustment: number;
  carriedIn: number;
  remaining: number;
}

export interface LeaveRequest {
  id: string;
  employeeId: string;
  employeeNameAr: string;
  employeeNameEn: string;
  leaveTypeId: string;
  leaveTypeNameAr: string;
  leaveTypeNameEn: string;
  startDate: string;
  endDate: string;
  isHalfDay: boolean;
  days: number;
  reason: string | null;
  attachmentFileId: string | null;
  status: string;
  workflowInstanceId: string | null;
  createdAt: string;
  rejectReason: string | null;
}

export interface PermissionRequest {
  id: string;
  employeeId: string;
  employeeNameAr: string;
  employeeNameEn: string;
  workDate: string;
  startTime: string;
  endTime: string;
  hours: number;
  reason: string | null;
  status: string;
  workflowInstanceId: string | null;
  createdAt: string;
  rejectReason: string | null;
}

export interface RequestInboxItem {
  taskId: string;
  instanceId: string;
  requestType: string;
  subjectId: string;
  requesterEmployeeId: string;
  requesterNameAr: string;
  requesterNameEn: string;
  stepOrder: number;
  createdAt: string;
  summary: string;
}

export interface WorkflowDefinition {
  id: string;
  nameAr: string;
  nameEn: string;
  requestType: string;
  isActive: boolean;
  isDefault: boolean;
  steps: {
    id: string;
    stepOrder: number;
    approverKind: string;
    roleId: string | null;
    userId: string | null;
    isFinal: boolean;
  }[];
}

@Injectable({ providedIn: 'root' })
export class LeaveApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}`;

  listTypes(): Promise<LeaveType[]> {
    return firstValueFrom(this.http.get<LeaveType[]>(`${this.base}/leave/types`));
  }

  listActiveTypes(): Promise<LeaveType[]> {
    return firstValueFrom(this.http.get<LeaveType[]>(`${this.base}/leave/types/active`));
  }

  updateType(
    id: string,
    body: {
      nameAr: string;
      nameEn: string;
      paid: boolean;
      daysPerYear: number | null;
      requiresApproval: boolean;
      requiresAttachment: boolean;
      isActive: boolean;
    },
  ): Promise<LeaveType> {
    return firstValueFrom(this.http.put<LeaveType>(`${this.base}/leave/types/${id}`, body));
  }

  myBalances(year?: number): Promise<LeaveBalance[]> {
    let params = new HttpParams();
    if (year != null) params = params.set('year', year);
    return firstValueFrom(this.http.get<LeaveBalance[]>(`${this.base}/leave/balances/me`, { params }));
  }

  listBalances(opts: { year?: number; employeeId?: string; leaveTypeId?: string } = {}): Promise<LeaveBalance[]> {
    let params = new HttpParams();
    if (opts.year != null) params = params.set('year', opts.year);
    if (opts.employeeId) params = params.set('employeeId', opts.employeeId);
    if (opts.leaveTypeId) params = params.set('leaveTypeId', opts.leaveTypeId);
    return firstValueFrom(this.http.get<LeaveBalance[]>(`${this.base}/leave/balances`, { params }));
  }

  adjustBalance(body: {
    employeeId: string;
    leaveTypeId: string;
    year: number;
    deltaDays: number;
    reason: string;
  }): Promise<LeaveBalance> {
    return firstValueFrom(this.http.post<LeaveBalance>(`${this.base}/leave/balances/adjust`, body));
  }

  createLeave(body: {
    leaveTypeId: string;
    startDate: string;
    endDate: string;
    isHalfDay: boolean;
    reason?: string | null;
    attachmentFileId?: string | null;
  }): Promise<LeaveRequest> {
    return firstValueFrom(this.http.post<LeaveRequest>(`${this.base}/leave/requests`, body));
  }

  uploadLeaveAttachment(file: File): Promise<{ fileId: string }> {
    const form = new FormData();
    form.append('file', file, file.name);
    return firstValueFrom(
      this.http.post<{ fileId: string }>(`${this.base}/leave/attachments`, form),
    );
  }

  myLeaveRequests(status?: string, page = 1, pageSize = 20): Promise<{
    items: LeaveRequest[];
    page: number;
    pageSize: number;
    totalCount: number;
  }> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return firstValueFrom(
      this.http.get<{
        items: LeaveRequest[];
        page: number;
        pageSize: number;
        totalCount: number;
      }>(`${this.base}/leave/requests/mine`, { params }),
    );
  }

  createPermission(body: {
    workDate: string;
    startTime: string;
    endTime: string;
    reason?: string | null;
  }): Promise<PermissionRequest> {
    return firstValueFrom(this.http.post<PermissionRequest>(`${this.base}/leave/permissions`, body));
  }

  myPermissions(): Promise<PermissionRequest[]> {
    return firstValueFrom(this.http.get<PermissionRequest[]>(`${this.base}/leave/permissions/mine`));
  }

  inbox(page = 1, pageSize = 20): Promise<{
    items: RequestInboxItem[];
    page: number;
    pageSize: number;
    totalCount: number;
  }> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return firstValueFrom(
      this.http.get<{
        items: RequestInboxItem[];
        page: number;
        pageSize: number;
        totalCount: number;
      }>(`${this.base}/requests/inbox`, { params }),
    );
  }

  approve(taskId: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.base}/requests/${taskId}/approve`, {}));
  }

  reject(taskId: string, reason: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.base}/requests/${taskId}/reject`, { reason }));
  }

  listWorkflows(): Promise<WorkflowDefinition[]> {
    return firstValueFrom(this.http.get<WorkflowDefinition[]>(`${this.base}/workflows`));
  }

  upsertWorkflow(
    id: string | null,
    body: {
      nameAr: string;
      nameEn: string;
      requestType: string;
      isActive: boolean;
      isDefault: boolean;
      steps: {
        stepOrder: number;
        approverKind: string;
        roleId?: string | null;
        userId?: string | null;
        isFinal: boolean;
      }[];
    },
  ): Promise<WorkflowDefinition> {
    if (id) {
      return firstValueFrom(this.http.put<WorkflowDefinition>(`${this.base}/workflows/${id}`, body));
    }
    return firstValueFrom(this.http.post<WorkflowDefinition>(`${this.base}/workflows`, body));
  }
}
