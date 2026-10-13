import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { FeatureKeys } from '../../core/subscription/feature-keys';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ThemeService } from '../../core/theme/theme.service';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { NavGroup, SidebarComponent } from './sidebar.component';

describe('SidebarComponent', () => {
  let permissions: string[];
  let features: Record<string, boolean>;

  beforeEach(async () => {
    permissions = [
      PermissionCodes.AttendanceReadOwn,
      PermissionCodes.LeaveReadOwn,
      PermissionCodes.LeaveReadTeam,
      PermissionCodes.TenantSettingsRead,
    ];
    features = { [FeatureKeys.Leave]: true, [FeatureKeys.Attendance]: true };

    await TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([{ path: 'leave/approvals', children: [] }]),
        provideTestTransloco(),
        {
          provide: SubscriptionStore,
          useValue: {
            hasFeature: (key: string) => !!features[key],
          },
        },
        {
          provide: AuthService,
          useValue: {
            me: signal({
              id: 'u1',
              fullName: 'Test',
              email: 't@example.com',
              language: 'ar',
              roles: ['Owner'],
              permissions,
              tenant: { id: 't1', name: 'شركة تجربة طويلة الاسم جداً للاختبار', slug: 'co', logoUrl: null },
              checklist: { inviteTeamDone: true },
              subscription: {
                state: 'trialRunning',
                trialEndsAt: null,
                daysLeft: 7,
                readOnly: false,
                features,
              },
            }),
            hasPermission: (code: string) => permissions.includes(code),
          },
        },
      ],
    }).compileComponents();
  });

  it('uses start-pointing collapse icon when expanded', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', false);
    fixture.detectChanges();

    const btn = fixture.nativeElement.querySelector('button[data-collapse-dir]') as HTMLElement;
    expect(btn.getAttribute('data-collapse-dir')).toBe('start');
    expect(btn.getAttribute('aria-expanded')).toBe('true');
    // Expanded: chevrons (points start with rtl-flip)
    expect(fixture.componentInstance['collapseIcon']()).toBe('chevrons');
    expect(fixture.componentInstance['collapseDir']()).toBe('start');
  });

  it('uses end-pointing collapse icon when collapsed', () => {
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', true);
    fixture.detectChanges();

    const btn = fixture.nativeElement.querySelector('button[data-collapse-dir]') as HTMLElement;
    expect(btn.getAttribute('data-collapse-dir')).toBe('end');
    expect(btn.getAttribute('aria-expanded')).toBe('false');
    expect(fixture.componentInstance['collapseIcon']()).toBe('chevron-end');
    expect(fixture.componentInstance['collapseDir']()).toBe('end');
  });

  it('keeps collapse direction semantics under rtl and ltr', () => {
    const theme = TestBed.inject(ThemeService);
    const fixture = TestBed.createComponent(SidebarComponent);

    theme.setLanguage('ar');
    fixture.componentRef.setInput('collapsed', false);
    fixture.detectChanges();
    expect(fixture.componentInstance['collapseDir']()).toBe('start');

    fixture.componentRef.setInput('collapsed', true);
    fixture.detectChanges();
    expect(fixture.componentInstance['collapseDir']()).toBe('end');

    theme.setLanguage('en');
    fixture.componentRef.setInput('collapsed', false);
    fixture.detectChanges();
    expect(fixture.componentInstance['collapseDir']()).toBe('start');
    expect(fixture.componentInstance['collapseIcon']()).toBe('chevrons');
  });

  it('marks Leave group active and open on /leave/approvals', async () => {
    const router = TestBed.inject(Router);
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', false);
    await router.navigateByUrl('/leave/approvals');
    fixture.detectChanges();

    const leave = fixture.componentInstance['entries']().find(
      (e): e is NavGroup => e.kind === 'group' && e.id === 'leave',
    );
    expect(leave).toBeTruthy();
    expect(fixture.componentInstance['isGroupActive'](leave!)).toBe(true);
    expect(fixture.componentInstance['isGroupOpen'](leave!)).toBe(true);

    const hrefs = Array.from(
      fixture.nativeElement.querySelectorAll('a[href]') as NodeListOf<HTMLAnchorElement>,
    ).map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('/leave/approvals');
    expect(hrefs.some((h) => h === '/leave/approvals' || h?.endsWith('/leave/approvals'))).toBe(
      true,
    );
  });

  it('hides Leave group when leave feature is off', () => {
    features[FeatureKeys.Leave] = false;
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.componentRef.setInput('collapsed', false);
    fixture.detectChanges();

    const leave = fixture.componentInstance['visibleEntries']().find(
      (e) => e.kind === 'group' && e.id === 'leave',
    );
    expect(leave).toBeUndefined();
  });
});
