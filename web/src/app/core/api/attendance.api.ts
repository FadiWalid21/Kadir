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
  photoFileId?: string | null;
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
  workLocationId?: string | null;
}

export interface WorkLocation {
  id: string;
  nameAr: string;
  nameEn: string | null;
  type: string;
  linkedBranchId: string | null;
  latitude: number;
  longitude: number;
  geofenceRadiusMeters: number;
  address: string | null;
  validFrom: string | null;
  validTo: string | null;
  isActive: boolean;
}

export interface PagedWorkLocations {
  items: WorkLocation[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface UpsertWorkLocationBody {
  nameAr: string;
  nameEn?: string | null;
  type: string;
  linkedBranchId?: string | null;
  latitude: number;
  longitude: number;
  geofenceRadiusMeters: number;
  address?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
  isActive: boolean;
}

export interface WorkLocationAssignment {
  id: string;
  scope: string;
  employeeId: string | null;
  departmentId: string | null;
  branchId: string | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
}

export interface WorkLocationAssignmentInput {
  scope: string;
  employeeId?: string | null;
  departmentId?: string | null;
  branchId?: string | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
}

export interface EmployeeAttendancePolicy {
  employeeId: string;
  mayClockInFromAnywhere: boolean;
  anywhereRequiresReason: boolean;
  anywhereRequiresPhoto: boolean;
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

export interface AttendanceBoardStats {
  present: number;
  late: number;
  absent: number;
  onLeave: number;
  notYet: number;
  total: number;
}

export interface AttendanceBoardRow {
  employeeId: string;
  employeeNumber: string;
  fullNameAr: string;
  fullNameEn: string;
  jobTitleAr: string | null;
  jobTitleEn: string | null;
  branchName: string;
  branchId: string;
  departmentId: string | null;
  departmentNameAr: string | null;
  departmentNameEn: string | null;
  status: string;
  firstInAt: string | null;
  lastOutAt: string | null;
  lateMinutes: number;
  workedMinutes: number;
  isInsideGeofence: boolean;
  distanceM: number | null;
  shiftNameAr: string | null;
  shiftNameEn: string | null;
  shiftStart: string | null;
  shiftEnd: string | null;
}

export interface AttendanceBoard {
  date: string;
  dayKind: string;
  stats: AttendanceBoardStats;
  items: AttendanceBoardRow[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ShiftAssignmentCell {
  employeeId: string;
  workDate: string;
  shiftId: string | null;
  shiftNameAr: string | null;
  shiftNameEn: string | null;
  startTime: string | null;
  endTime: string | null;
  isDayOff: boolean;
  isHoliday: boolean;
  hasConflict: boolean;
}

export interface ShiftScheduleEmployee {
  employeeId: string;
  employeeNumber: string;
  fullNameAr: string;
  fullNameEn: string;
  jobTitleAr: string | null;
  jobTitleEn: string | null;
  days: ShiftAssignmentCell[];
}

export interface ShiftScheduleWeek {
  weekStart: string;
  weekEnd: string;
  employees: ShiftScheduleEmployee[];
  scheduledShifts: number;
  restDays: number;
  conflicts: number;
}

export interface ShiftAssignmentRule {
  id: string;
  targetType: string;
  targetId: string;
  targetName: string | null;
  shiftId: string | null;
  shiftNameAr: string | null;
  shiftNameEn: string | null;
  isDayOff: boolean;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface PagedShiftAssignmentRules {
  items: ShiftAssignmentRule[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateShiftAssignmentRulesBody {
  targetType: string;
  targetIds: string[];
  shiftId: string | null;
  isDayOff: boolean;
  effectiveFrom: string;
  effectiveTo: string | null;
  confirmRecalc?: boolean;
}

export interface UpdateShiftAssignmentRuleBody {
  shiftId: string | null;
  isDayOff: boolean;
  effectiveFrom: string;
  effectiveTo: string | null;
  confirmRecalc?: boolean;
}

export interface PreviewShiftAssignmentRulesBody {
  targetType: string;
  targetIds: string[];
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface PreviewShiftAssignmentRules {
  affectedEmployeeCount: number;
  overrideCount: number;
  note: string;
}

export interface AttendanceMatrix {
  year: number;
  month: number;
  commitmentPercent: number;
  totalLateMinutes: number;
  totalOvertimeMinutes: number;
  items: {
    employeeId: string;
    employeeNumber: string;
    fullNameAr: string;
    fullNameEn: string;
    jobTitleAr: string | null;
    jobTitleEn: string | null;
    presentDays: number;
    days: { date: string; status: string; lateMinutes: number }[];
  }[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface AttendanceCorrection {
  id: string;
  employeeId: string;
  employeeNameAr: string;
  employeeNameEn: string;
  workDate: string;
  problemType: string;
  proposedTime: string;
  reason: string;
  attachmentFileId: string | null;
  status: string;
  rejectReason: string | null;
  createdAt: string;
  decidedAt: string | null;
  decidedByName: string | null;
}

export interface PagedCorrections {
  items: AttendanceCorrection[];
  page: number;
  pageSize: number;
  totalCount: number;
  pendingCount: number;
}

@Injectable({ providedIn: 'root' })
export class AttendanceApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/attendance`;

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

  getBoardToday(params: {
    date?: string;
    branchId?: string;
    departmentId?: string;
    status?: string;
    q?: string;
    page?: number;
    pageSize?: number;
  }): Promise<AttendanceBoard> {
    let httpParams = new HttpParams();
    if (params.date) httpParams = httpParams.set('date', params.date);
    if (params.branchId) httpParams = httpParams.set('branchId', params.branchId);
    if (params.departmentId) httpParams = httpParams.set('departmentId', params.departmentId);
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.q) httpParams = httpParams.set('q', params.q);
    if (params.page != null) httpParams = httpParams.set('page', params.page);
    if (params.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize);
    return firstValueFrom(this.http.get<AttendanceBoard>(`${this.base}/today`, { params: httpParams }));
  }

  getScheduleWeek(weekStart: string, branchId?: string, departmentId?: string): Promise<ShiftScheduleWeek> {
    let params = new HttpParams().set('weekStart', weekStart);
    if (branchId) params = params.set('branchId', branchId);
    if (departmentId) params = params.set('departmentId', departmentId);
    return firstValueFrom(this.http.get<ShiftScheduleWeek>(`${this.base}/schedules/week`, { params }));
  }

  upsertAssignments(
    items: { employeeId: string; workDate: string; shiftId: string | null; isDayOff: boolean }[],
  ): Promise<ShiftScheduleWeek> {
    return firstValueFrom(
      this.http.put<ShiftScheduleWeek>(`${this.base}/schedules/assignments`, { items }),
    );
  }

  listShiftRules(params?: {
    page?: number;
    pageSize?: number;
    targetType?: string;
  }): Promise<PagedShiftAssignmentRules> {
    let httpParams = new HttpParams();
    if (params?.page != null) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize);
    if (params?.targetType) httpParams = httpParams.set('targetType', params.targetType);
    return firstValueFrom(
      this.http.get<PagedShiftAssignmentRules>(`${this.base}/shift-rules`, { params: httpParams }),
    );
  }

  createShiftRules(body: CreateShiftAssignmentRulesBody): Promise<ShiftAssignmentRule[]> {
    return firstValueFrom(
      this.http.post<ShiftAssignmentRule[]>(`${this.base}/shift-rules`, body),
    );
  }

  previewShiftRules(body: PreviewShiftAssignmentRulesBody): Promise<PreviewShiftAssignmentRules> {
    return firstValueFrom(
      this.http.post<PreviewShiftAssignmentRules>(`${this.base}/shift-rules/preview`, body),
    );
  }

  updateShiftRule(id: string, body: UpdateShiftAssignmentRuleBody): Promise<ShiftAssignmentRule> {
    return firstValueFrom(
      this.http.put<ShiftAssignmentRule>(`${this.base}/shift-rules/${id}`, body),
    );
  }

  endShiftRule(id: string, effectiveTo: string): Promise<ShiftAssignmentRule> {
    return firstValueFrom(
      this.http.post<ShiftAssignmentRule>(`${this.base}/shift-rules/${id}/end`, { effectiveTo }),
    );
  }

  getMatrix(params: {
    year: number;
    month: number;
    branchId?: string;
    departmentId?: string;
    q?: string;
    page?: number;
    pageSize?: number;
  }): Promise<AttendanceMatrix> {
    let httpParams = new HttpParams()
      .set('year', params.year)
      .set('month', params.month);
    if (params.branchId) httpParams = httpParams.set('branchId', params.branchId);
    if (params.departmentId) httpParams = httpParams.set('departmentId', params.departmentId);
    if (params.q) httpParams = httpParams.set('q', params.q);
    if (params.page != null) httpParams = httpParams.set('page', params.page);
    if (params.pageSize != null) httpParams = httpParams.set('pageSize', params.pageSize);
    return firstValueFrom(
      this.http.get<AttendanceMatrix>(`${this.base}/reports/matrix`, { params: httpParams }),
    );
  }

  createCorrection(body: {
    workDate: string;
    problemType: string;
    /** Time-only (HH:mm or HH:mm:ss), not a full ISO datetime. */
    proposedTime: string;
    reason: string;
    attachmentFileId?: string | null;
  }): Promise<AttendanceCorrection> {
    return firstValueFrom(this.http.post<AttendanceCorrection>(`${this.base}/corrections`, body));
  }

  listMyCorrections(status?: string, page = 1, pageSize = 20): Promise<PagedCorrections> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return firstValueFrom(
      this.http.get<PagedCorrections>(`${this.base}/corrections/mine`, { params }),
    );
  }

  listCorrectionsInbox(status?: string, page = 1, pageSize = 20): Promise<PagedCorrections> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return firstValueFrom(this.http.get<PagedCorrections>(`${this.base}/corrections`, { params }));
  }

  approveCorrection(id: string): Promise<AttendanceCorrection> {
    return firstValueFrom(
      this.http.post<AttendanceCorrection>(`${this.base}/corrections/${id}/approve`, {}),
    );
  }

  rejectCorrection(id: string, reason: string): Promise<AttendanceCorrection> {
    return firstValueFrom(
      this.http.post<AttendanceCorrection>(`${this.base}/corrections/${id}/reject`, { reason }),
    );
  }

  listWorkLocations(opts: {
    type?: string;
    active?: boolean;
    q?: string;
    page?: number;
    pageSize?: number;
  } = {}): Promise<PagedWorkLocations> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 25));
    if (opts.type) params = params.set('type', opts.type);
    if (opts.active != null) params = params.set('active', String(opts.active));
    if (opts.q) params = params.set('q', opts.q);
    return firstValueFrom(
      this.http.get<PagedWorkLocations>(`${this.base}/work-locations`, { params }),
    );
  }

  getWorkLocation(id: string): Promise<WorkLocation> {
    return firstValueFrom(this.http.get<WorkLocation>(`${this.base}/work-locations/${id}`));
  }

  createWorkLocation(body: UpsertWorkLocationBody): Promise<WorkLocation> {
    return firstValueFrom(this.http.post<WorkLocation>(`${this.base}/work-locations`, body));
  }

  updateWorkLocation(id: string, body: UpsertWorkLocationBody): Promise<WorkLocation> {
    return firstValueFrom(this.http.put<WorkLocation>(`${this.base}/work-locations/${id}`, body));
  }

  getWorkLocationAssignments(id: string): Promise<WorkLocationAssignment[]> {
    return firstValueFrom(
      this.http.get<WorkLocationAssignment[]>(`${this.base}/work-locations/${id}/assignments`),
    );
  }

  replaceWorkLocationAssignments(
    id: string,
    assignments: WorkLocationAssignmentInput[],
  ): Promise<WorkLocationAssignment[]> {
    return firstValueFrom(
      this.http.put<WorkLocationAssignment[]>(`${this.base}/work-locations/${id}/assignments`, {
        assignments,
      }),
    );
  }

  getEmployeeAttendancePolicy(employeeId: string): Promise<EmployeeAttendancePolicy> {
    return firstValueFrom(
      this.http.get<EmployeeAttendancePolicy>(
        `${this.base}/employees/${employeeId}/attendance-policy`,
      ),
    );
  }

  updateEmployeeAttendancePolicy(
    employeeId: string,
    body: Omit<EmployeeAttendancePolicy, 'employeeId'>,
  ): Promise<EmployeeAttendancePolicy> {
    return firstValueFrom(
      this.http.put<EmployeeAttendancePolicy>(
        `${this.base}/employees/${employeeId}/attendance-policy`,
        body,
      ),
    );
  }
}
