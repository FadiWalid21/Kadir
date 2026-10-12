import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AttendanceApi } from '../core/api/attendance.api';
import { HomeApi, HomeDashboardDto } from '../core/api/home.api';
import { AuthService } from '../core/auth/auth.service';
import { InstallPromptService } from '../core/pwa/install-prompt.service';
import { provideTestTransloco } from '../testing/transloco-testing';
import { HomePage } from './home.page';

const me = {
  id: 'u1',
  fullName: 'مالك الاختبار',
  email: 'owner@example.com',
  language: 'ar',
  roles: ['Owner'],
  permissions: [] as string[],
  tenant: { id: 't1', name: 'شركة الاختبار', slug: 'test-co', logoUrl: null },
  checklist: { inviteTeamDone: false },
};

const dashboard: HomeDashboardDto = {
  greeting: { fullName: 'مالك الاختبار', tenantName: 'شركة الاختبار', logoUrl: null },
  checklist: {
    doneCount: 0,
    totalCount: 4,
    isComplete: false,
    items: [
      { key: 'logo', done: false, linkPath: '/settings/company' },
      { key: 'invite', done: false, linkPath: '/settings/users' },
      { key: 'employee', done: false, linkPath: '/employees' },
      { key: 'shifts', done: false, linkPath: '/settings/templates' },
    ],
  },
  employee: null,
  manager: {
    pendingApprovalCount: 0,
    pendingApprovals: [],
    teamToday: { present: 0, late: 0, absent: 0, onLeave: 0 },
  },
  hr: {
    headcount: 0,
    lateToday: 0,
    absentToday: 0,
    pendingWorkflowCount: 0,
    expiringDocuments: [],
  },
};

describe('HomePage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        Title,
        {
          provide: AuthService,
          useValue: {
            me: signal(me),
            status: signal('authenticated'),
            restoreSession: vi.fn().mockResolvedValue(undefined),
          },
        },
        {
          provide: HomeApi,
          useValue: { getDashboard: vi.fn().mockResolvedValue(dashboard) },
        },
        {
          provide: InstallPromptService,
          useValue: {
            showBanner: signal(false),
            showMenuItem: signal(false),
            canInstall: signal(false),
            dismissBanner: vi.fn(),
            promptInstall: vi.fn(),
          },
        },
        {
          provide: AttendanceApi,
          useValue: {
            getMyToday: vi.fn().mockResolvedValue({
              workDate: '2026-10-12',
              dayKind: 'Workday',
              shift: null,
              branch: {
                id: 'b1',
                name: 'HQ',
                latitude: null,
                longitude: null,
                geofenceRadiusMeters: null,
                outOfRangePolicy: 'Block',
                isConfigured: false,
              },
              openPunch: null,
              punches: [],
              dayStatus: null,
              lateMinutes: null,
              canPunch: false,
            }),
          },
        },
      ],
    }).compileComponents();
  });

  it('renders checklist and persona sections in Arabic', async () => {
    const fixture = TestBed.createComponent(HomePage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.componentInstance;
    // Ensure payload is present even if async load raced.
    if (!page['dashboard']()) {
      page.setDashboardForTest(dashboard);
      fixture.detectChanges();
    }

    let text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('جاهزية مكان العمل');
    expect(text).toContain('إضافة شعار الشركة');
    expect(text).toContain('إضافة أول موظف');
    expect(text).toContain('0 / 4');
    expect(text).toContain('الموافقات');
    expect(fixture.nativeElement.querySelector('a[href="/settings/company"]')).toBeTruthy();

    page.setStateForTest('loading');
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('kadr-skeleton')).toBeTruthy();

    page.setStateForTest('error');
    fixture.detectChanges();
    text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('تعذر تحميل الصفحة');
  });
});
