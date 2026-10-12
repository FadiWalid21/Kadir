import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface HomeDashboardDto {
  greeting: { fullName: string; tenantName: string; logoUrl: string | null };
  checklist: {
    doneCount: number;
    totalCount: number;
    isComplete: boolean;
    items: { key: string; done: boolean; linkPath: string }[];
  } | null;
  employee: {
    punch: {
      canPunch: boolean;
      nextAction: string | null;
      lastPunchAt: string | null;
      status: string | null;
    } | null;
    leaveBalances: {
      leaveTypeId: string;
      nameAr: string;
      nameEn: string;
      remaining: number;
      entitled: number;
    }[];
    myPendingRequests: {
      id: string;
      kind: string;
      status: string;
      startDate: string | null;
      endDate: string | null;
      typeNameAr: string | null;
      typeNameEn: string | null;
    }[];
  } | null;
  manager: {
    pendingApprovalCount: number;
    pendingApprovals: {
      taskId: string;
      requestType: string;
      employeeNameAr: string;
      employeeNameEn: string | null;
      createdAt: string;
    }[];
    teamToday: { present: number; late: number; absent: number; onLeave: number };
  } | null;
  hr: {
    headcount: number;
    lateToday: number;
    absentToday: number;
    pendingWorkflowCount: number;
    expiringDocuments: {
      documentId: string;
      employeeId: string;
      employeeNameAr: string;
      employeeNameEn: string | null;
      documentTypeAr: string;
      documentTypeEn: string | null;
      expiryDate: string | null;
    }[];
  } | null;
}

@Injectable({ providedIn: 'root' })
export class HomeApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/home`;

  getDashboard(): Promise<HomeDashboardDto> {
    return firstValueFrom(
      this.http.get<HomeDashboardDto>(`${this.base}/dashboard`, { withCredentials: true }),
    );
  }
}
