import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { ThemeService } from '../../core/theme/theme.service';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { SidebarComponent } from './sidebar.component';

describe('SidebarComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        provideTestTransloco(),
        {
          provide: AuthService,
          useValue: {
            me: signal({
              id: 'u1',
              fullName: 'Test',
              email: 't@example.com',
              language: 'ar',
              roles: ['Owner'],
              permissions: ['attendance.read.own', 'leave.read.own', 'tenant.settings.read'],
              tenant: { id: 't1', name: 'شركة تجربة طويلة الاسم جداً للاختبار', slug: 'co', logoUrl: null },
              checklist: { inviteTeamDone: true },
              subscription: {
                state: 'trialRunning',
                trialEndsAt: null,
                daysLeft: 7,
                readOnly: false,
                features: {},
              },
            }),
            hasPermission: () => true,
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
});
