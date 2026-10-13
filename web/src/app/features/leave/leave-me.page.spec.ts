import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LeaveApi } from '../../core/api/leave.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { LeaveMePage } from './leave-me.page';

describe('LeaveMePage', () => {
  const myBalances = vi.fn();
  const listActiveTypes = vi.fn();
  let permissions: string[];

  beforeEach(async () => {
    permissions = [PermissionCodes.LeaveReadOwn, PermissionCodes.LeaveRequest];
    myBalances.mockReset();
    listActiveTypes.mockReset();
    listActiveTypes.mockResolvedValue([]);

    await TestBed.configureTestingModule({
      imports: [LeaveMePage],
      providers: [
        provideTestTransloco(),
        { provide: Title, useValue: { setTitle: vi.fn() } },
        { provide: ToastService, useValue: { show: vi.fn() } },
        {
          provide: LeaveApi,
          useValue: { myBalances, listActiveTypes, createLeave: vi.fn() },
        },
        {
          provide: AuthService,
          useValue: {
            me: signal(null),
            hasPermission: (code: string) => permissions.includes(code),
          },
        },
      ],
    }).compileComponents();
  });

  it('shows unlinked-employee empty state instead of network error', async () => {
    myBalances.mockRejectedValue(
      new HttpErrorResponse({
        status: 400,
        error: { code: 'attendance.no_employee', title: 'No employee' },
      }),
    );

    const fixture = TestBed.createComponent(LeaveMePage);
    fixture.detectChanges();
    await fixture.componentInstance['reload']();
    fixture.detectChanges();

    expect(fixture.componentInstance['state']()).toBe('noEmployee');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toMatch(/موظف|employee/i);
    expect(fixture.nativeElement.querySelector('kadr-network-error')).toBeNull();
  });

  it('hides request CTA without leave.request permission', async () => {
    permissions = [PermissionCodes.LeaveReadOwn];
    myBalances.mockResolvedValue([
      {
        employeeId: 'e1',
        employeeNameAr: 'أ',
        employeeNameEn: 'A',
        leaveTypeId: 't1',
        leaveTypeNameAr: 'سنوية',
        leaveTypeNameEn: 'Annual',
        year: 2026,
        entitled: 21,
        accrued: 21,
        used: 0,
        adjustment: 0,
        carriedIn: 0,
        remaining: 21,
      },
    ]);

    const fixture = TestBed.createComponent(LeaveMePage);
    fixture.detectChanges();
    await fixture.componentInstance['reload']();
    fixture.detectChanges();

    expect(fixture.componentInstance['canRequest']()).toBe(false);
    expect(fixture.componentInstance['state']()).toBe('ready');
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('kadr-button') as NodeListOf<HTMLElement>,
    );
    expect(buttons.some((b) => (b.textContent ?? '').includes('طلب') || (b.textContent ?? '').toLowerCase().includes('request'))).toBe(
      false,
    );
  });
});
