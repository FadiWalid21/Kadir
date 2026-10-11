import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PunchRequest {
  latitude: number;
  longitude: number;
  accuracyM: number;
  positionAgeMs: number;
  clientRequestId: string;
  reason?: string | null;
}

export interface PunchResult {
  punchId: string;
  kind: string;
  punchedAt: string;
  workDate: string;
  isInside: boolean;
  distanceM: number;
  outsideReason: string | null;
  dayStatus: string;
}

export interface AttendancePunch {
  id: string;
  kind: string;
  punchedAt: string;
  isInside: boolean;
  distanceM: number | null;
  outsideReason: string | null;
}

export interface AttendanceShift {
  id: string | null;
  nameAr: string | null;
  nameEn: string | null;
  startTime: string | null;
  endTime: string | null;
  crossesMidnight: boolean;
}

export interface AttendanceBranchGeo {
  id: string;
  name: string;
  latitude: number | null;
  longitude: number | null;
  geofenceRadiusMeters: number | null;
  outOfRangePolicy: string;
  isConfigured: boolean;
}

export interface AttendanceToday {
  workDate: string;
  dayKind: string;
  shift: AttendanceShift | null;
  branch: AttendanceBranchGeo;
  openPunch: AttendancePunch | null;
  punches: AttendancePunch[];
  dayStatus: string | null;
  lateMinutes: number | null;
  canPunch: boolean;
}

export interface AttendanceMonthDay {
  date: string;
  status: string;
  workedMinutes: number;
  lateMinutes: number;
  earlyLeaveMinutes: number;
  overtimeMinutes: number;
  firstInAt: string | null;
  lastOutAt: string | null;
}

export interface AttendanceMonth {
  year: number;
  month: number;
  days: AttendanceMonthDay[];
  presentDays: number;
  lateDays: number;
  absentDays: number;
  incompleteDays: number;
  totalWorkedMinutes: number;
  totalLateMinutes: number;
  totalOvertimeMinutes: number;
}

export interface AttendanceRules {
  graceLateMinutes: number;
  graceEarlyLeaveMinutes: number;
  absentAfterStartHours: number;
  overtimeEnabled: boolean;
  overtimeAfterEndMinutes: number;
  overtimeMinMinutes: number;
  locationAccuracyM: number;
}

export interface AttendanceDefaultShift {
  shiftId: string | null;
  nameAr: string | null;
  nameEn: string | null;
}

export interface BranchLocation {
  branchId: string;
  name: string;
  latitude: number | null;
  longitude: number | null;
  geofenceRadiusMeters: number | null;
  outOfRangePolicy: string;
  isConfigured: boolean;
}

export interface Holiday {
  id: string;
  startsOn: string;
  endsOn: string;
  nameAr: string;
  nameEn: string;
  isOfficial: boolean;
  repeatsYearly: boolean;
  isActive: boolean;
}

export interface WeekendSettings {
  weekendDays: string[];
}

@Injectable({ providedIn: 'root' })
export class AttendanceApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/v1/attendance`;

  punch(body: PunchRequest): Promise<PunchResult> {
    return firstValueFrom(this.http.post<PunchResult>(`${this.base}/punch`, body));
  }

  getMyToday(): Promise<AttendanceToday> {
    return firstValueFrom(this.http.get<AttendanceToday>(`${this.base}/me/today`));
  }

  getMyMonth(year: number, month: number): Promise<AttendanceMonth> {
    const params = new HttpParams().set('year', year).set('month', month);
    return firstValueFrom(this.http.get<AttendanceMonth>(`${this.base}/me/month`, { params }));
  }

  getRules(): Promise<AttendanceRules> {
    return firstValueFrom(this.http.get<AttendanceRules>(`${this.base}/settings/rules`));
  }

  updateRules(body: AttendanceRules): Promise<AttendanceRules> {
    return firstValueFrom(this.http.put<AttendanceRules>(`${this.base}/settings/rules`, body));
  }

  getDefaultShift(): Promise<AttendanceDefaultShift> {
    return firstValueFrom(
      this.http.get<AttendanceDefaultShift>(`${this.base}/settings/default-shift`),
    );
  }

  setDefaultShift(shiftId: string | null): Promise<AttendanceDefaultShift> {
    return firstValueFrom(
      this.http.put<AttendanceDefaultShift>(`${this.base}/settings/default-shift`, { shiftId }),
    );
  }

  listBranches(): Promise<BranchLocation[]> {
    return firstValueFrom(this.http.get<BranchLocation[]>(`${this.base}/settings/branches`));
  }

  updateBranchLocation(
    branchId: string,
    body: {
      latitude: number;
      longitude: number;
      geofenceRadiusMeters: number;
      outOfRangePolicy: string;
    },
  ): Promise<BranchLocation> {
    return firstValueFrom(
      this.http.put<BranchLocation>(`${this.base}/settings/branches/${branchId}/location`, body),
    );
  }

  listHolidays(year?: number): Promise<Holiday[]> {
    let params = new HttpParams();
    if (year != null) {
      params = params.set('year', year);
    }
    return firstValueFrom(this.http.get<Holiday[]>(`${this.base}/settings/holidays`, { params }));
  }

  createHoliday(body: Omit<Holiday, 'id'>): Promise<Holiday> {
    return firstValueFrom(this.http.post<Holiday>(`${this.base}/settings/holidays`, body));
  }

  updateHoliday(id: string, body: Omit<Holiday, 'id'>): Promise<Holiday> {
    return firstValueFrom(this.http.put<Holiday>(`${this.base}/settings/holidays/${id}`, body));
  }

  deleteHoliday(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/settings/holidays/${id}`));
  }

  getWeekend(): Promise<WeekendSettings> {
    return firstValueFrom(this.http.get<WeekendSettings>(`${this.base}/settings/weekend`));
  }

  updateWeekend(weekendDays: string[]): Promise<WeekendSettings> {
    return firstValueFrom(
      this.http.put<WeekendSettings>(`${this.base}/settings/weekend`, { weekendDays }),
    );
  }
}
