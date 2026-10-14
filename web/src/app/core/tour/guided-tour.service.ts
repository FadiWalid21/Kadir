import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { PermissionCodes } from '../auth/permission-codes';

export interface TourStepDef {
  id: string;
  /** Matches [data-tour="…"] in the shell. */
  target: string;
  titleKey: string;
  bodyKey: string;
  /** When set, step is skipped if the user lacks the permission. */
  permission?: string;
  /** Prefer mobile bottom-nav target when viewport is narrow. */
  mobileTarget?: string;
}

const STORAGE_PREFIX = 'kadr.tour.v1.';

const STEPS: TourStepDef[] = [
  {
    id: 'home',
    target: 'nav-home',
    mobileTarget: 'bottom-home',
    titleKey: 'tour.steps.homeTitle',
    bodyKey: 'tour.steps.homeBody',
  },
  {
    id: 'people',
    target: 'nav-people',
    mobileTarget: 'bottom-more',
    titleKey: 'tour.steps.peopleTitle',
    bodyKey: 'tour.steps.peopleBody',
    permission: PermissionCodes.EmployeesRead,
  },
  {
    id: 'attendance',
    target: 'nav-attendance',
    mobileTarget: 'bottom-attendance',
    titleKey: 'tour.steps.attendanceTitle',
    bodyKey: 'tour.steps.attendanceBody',
    permission: PermissionCodes.AttendanceReadOwn,
  },
  {
    id: 'leave',
    target: 'nav-leave',
    mobileTarget: 'bottom-approvals',
    titleKey: 'tour.steps.leaveTitle',
    bodyKey: 'tour.steps.leaveBody',
    permission: PermissionCodes.LeaveReadOwn,
  },
  {
    id: 'notifications',
    target: 'top-notifications',
    titleKey: 'tour.steps.notificationsTitle',
    bodyKey: 'tour.steps.notificationsBody',
  },
  {
    id: 'settings',
    target: 'nav-settings',
    mobileTarget: 'bottom-more',
    titleKey: 'tour.steps.settingsTitle',
    bodyKey: 'tour.steps.settingsBody',
    permission: PermissionCodes.TenantSettingsRead,
  },
];

@Injectable({ providedIn: 'root' })
export class GuidedTourService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly auth = inject(AuthService);

  private readonly activeSignal = signal(false);
  private readonly indexSignal = signal(0);
  private readonly stepsSignal = signal<TourStepDef[]>([]);

  readonly active = this.activeSignal.asReadonly();
  readonly index = this.indexSignal.asReadonly();
  readonly steps = this.stepsSignal.asReadonly();
  readonly current = computed(() => {
    const list = this.stepsSignal();
    const i = this.indexSignal();
    return list[i] ?? null;
  });
  readonly isLast = computed(() => {
    const list = this.stepsSignal();
    return list.length > 0 && this.indexSignal() >= list.length - 1;
  });

  /** Call once from the authenticated shell. */
  maybeStart(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    if (this.auth.status() !== 'authenticated') {
      return;
    }
    const userId = this.auth.me()?.id;
    if (!userId || this.hasCompleted(userId)) {
      return;
    }
    const steps = this.resolveSteps();
    if (steps.length === 0) {
      this.markCompleted(userId);
      return;
    }
    this.stepsSignal.set(steps);
    this.indexSignal.set(0);
    this.activeSignal.set(true);
  }

  /** Ui-kit / tests: force-show without persisting completion. */
  preview(): void {
    this.stepsSignal.set(this.resolveSteps().length ? this.resolveSteps() : STEPS);
    this.indexSignal.set(0);
    this.activeSignal.set(true);
  }

  next(): void {
    if (this.isLast()) {
      this.complete();
      return;
    }
    this.indexSignal.update((i) => i + 1);
  }

  skip(): void {
    this.complete();
  }

  complete(): void {
    this.activeSignal.set(false);
    const userId = this.auth.me()?.id;
    if (userId) {
      this.markCompleted(userId);
    }
  }

  /** Test hook. */
  hasCompletedForTests(userId: string): boolean {
    return this.hasCompleted(userId);
  }

  resolveTargetSelector(step: TourStepDef): string {
    const mobile = this.isNarrow() && step.mobileTarget ? step.mobileTarget : step.target;
    return `[data-tour="${mobile}"]`;
  }

  private resolveSteps(): TourStepDef[] {
    return STEPS.filter((s) => {
      if (s.permission && !this.auth.hasPermission(s.permission)) {
        return false;
      }
      const selector = this.resolveTargetSelector(s);
      return !!this.document.querySelector(selector);
    });
  }

  private isNarrow(): boolean {
    const w = this.document.defaultView;
    if (!w || typeof w.matchMedia !== 'function') {
      return false;
    }
    return w.matchMedia('(max-width: 767px)').matches;
  }

  private storageKey(userId: string): string {
    return `${STORAGE_PREFIX}${userId}`;
  }

  private hasCompleted(userId: string): boolean {
    try {
      return localStorage.getItem(this.storageKey(userId)) === '1';
    } catch {
      return false;
    }
  }

  private markCompleted(userId: string): void {
    try {
      localStorage.setItem(this.storageKey(userId), '1');
    } catch {
      /* ignore */
    }
  }
}
