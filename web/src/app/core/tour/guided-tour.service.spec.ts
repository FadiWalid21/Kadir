import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/common';
import { signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { GuidedTourService } from './guided-tour.service';

describe('GuidedTourService', () => {
  const me = signal<{ id: string; permissions: string[] } | null>(null);
  const status = signal<'authenticated' | 'anonymous'>('authenticated');

  beforeEach(() => {
    localStorage.clear();
    me.set({ id: 'user-1', permissions: ['employees.read', 'attendance.read.own', 'leave.read.own', 'tenant.settings.read'] });
    status.set('authenticated');
    TestBed.configureTestingModule({
      providers: [
        GuidedTourService,
        {
          provide: AuthService,
          useValue: {
            me: me.asReadonly(),
            status: status.asReadonly(),
            hasPermission: (code: string) => !!me()?.permissions.includes(code),
          },
        },
      ],
    });
    const doc = TestBed.inject(DOCUMENT);
    doc.body.innerHTML = `
      <a data-tour="nav-home"></a>
      <button data-tour="nav-people"></button>
      <button data-tour="nav-attendance"></button>
      <button data-tour="nav-leave"></button>
      <span data-tour="top-notifications"></span>
      <a data-tour="nav-settings"></a>
    `;
  });

  it('does not start when storage already completed', () => {
    localStorage.setItem('kadr.tour.v1.user-1', '1');
    const tour = TestBed.inject(GuidedTourService);
    tour.maybeStart();
    expect(tour.active()).toBe(false);
  });

  it('starts and advances then completes', () => {
    const tour = TestBed.inject(GuidedTourService);
    tour.maybeStart();
    expect(tour.active()).toBe(true);
    expect(tour.current()?.id).toBe('home');
    tour.next();
    expect(tour.current()?.id).toBe('people');
    while (!tour.isLast()) {
      tour.next();
    }
    tour.next();
    expect(tour.active()).toBe(false);
    expect(tour.hasCompletedForTests('user-1')).toBe(true);
  });

  it('skip marks completed', () => {
    const tour = TestBed.inject(GuidedTourService);
    tour.maybeStart();
    tour.skip();
    expect(tour.active()).toBe(false);
    expect(localStorage.getItem('kadr.tour.v1.user-1')).toBe('1');
  });
});
