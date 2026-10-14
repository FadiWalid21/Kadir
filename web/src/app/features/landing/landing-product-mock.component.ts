import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';

type ScenePhase = 'clock' | 'leave' | 'doc' | 'done';

@Component({
  selector: 'kadr-landing-product-mock',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, BadgeComponent, ButtonComponent, IconComponent],
  template: `
    <div
      class="relative isolate overflow-hidden rounded-lg border border-line bg-surface p-4 shadow-md"
      role="img"
      [attr.aria-label]="'landing.hero.mockAttendance' | transloco"
    >
      <!-- Decorative shapes stay fully behind the card content -->
      <div class="pointer-events-none absolute inset-0 -z-10 overflow-hidden" aria-hidden="true">
        <div
          class="absolute start-4 top-6 size-28 rounded-full border border-accent/20 motion-safe:transition-transform motion-safe:duration-[var(--dur-reveal-long)]"
          [class.scale-110]="ripple()"
        ></div>
        <div class="absolute start-8 top-10 size-20 rounded-full border border-accent/15"></div>
        <svg class="absolute end-6 bottom-8 size-16 text-line opacity-70" viewBox="0 0 48 48" aria-hidden="true">
          @for (y of [6, 14, 22, 30, 38]; track y) {
            @for (x of [6, 14, 22, 30, 38]; track x) {
              <circle [attr.cx]="x" [attr.cy]="y" r="1.2" fill="currentColor" />
            }
          }
        </svg>
        <svg class="absolute -start-2 top-1/3 size-24 text-accent/10" viewBox="0 0 80 80">
          <path d="M8 40a32 32 0 0 1 64 0" fill="none" stroke="currentColor" stroke-width="1.5" />
          <path d="M16 40a24 24 0 0 1 48 0" fill="none" stroke="currentColor" stroke-width="1.5" />
        </svg>
      </div>

      <div class="relative z-10 flex flex-col gap-3">
        <div class="flex items-center justify-between gap-2">
          <p class="text-sm font-medium text-fg">
            {{ 'landing.hero.greeting' | transloco: { name: ('landing.hero.employeeName' | transloco) } }}
          </p>
          <button
            type="button"
            class="inline-flex size-9 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
            [attr.aria-label]="(paused() ? 'landing.hero.play' : 'landing.hero.pause') | transloco"
            (click)="togglePause()"
          >
            <kadr-icon [name]="paused() ? 'play' : 'pause'" size="sm" />
          </button>
        </div>

        <!-- Scene 1: clock-in -->
        <div class="rounded-md border border-line bg-app p-3">
          <div class="flex items-center justify-between gap-3">
            <div class="text-start">
              <p class="text-xs text-fg-2">{{ 'landing.hero.mockAttendance' | transloco }}</p>
              <p class="text-2xl font-bold tabular-nums text-fg">{{ attendanceCount() }}</p>
            </div>
            <div class="relative flex flex-col items-center gap-2">
              <div class="relative size-14">
                <svg class="size-14 -rotate-90 text-accent" viewBox="0 0 36 36" aria-hidden="true">
                  <circle cx="18" cy="18" r="15" fill="none" class="text-line" stroke="currentColor" stroke-width="3" />
                  <circle
                    cx="18"
                    cy="18"
                    r="15"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="3"
                    stroke-linecap="round"
                    class="motion-safe:transition-[stroke-dasharray] motion-safe:duration-[var(--dur-reveal-long)] motion-safe:ease-[var(--ease)]"
                    [attr.stroke-dasharray]="ringDash()"
                  />
                </svg>
                <span
                  class="absolute inset-0 flex items-center justify-center text-accent motion-safe:transition-transform motion-safe:duration-[var(--dur)]"
                  [class.scale-110]="ripple()"
                >
                  <kadr-icon name="map-pin" size="sm" />
                </span>
              </div>
              @if (showInsidePill()) {
                <span
                  class="rounded-full bg-success-soft px-2 py-0.5 text-xs font-medium text-success motion-safe:animate-[kadr-fade_var(--dur)_ease-out]"
                >
                  {{ 'landing.hero.insideGeofence' | transloco }}
                </span>
              }
            </div>
          </div>
          <div class="mt-3 flex items-center gap-2">
            <kadr-button
              variant="primary"
              size="sm"
              type="button"
              [class.opacity-80]="clockPressed()"
              (click)="noop()"
            >
              {{ 'landing.hero.clockIn' | transloco }}
            </kadr-button>
            @if (showToast()) {
              <span
                class="inline-flex items-center gap-1 rounded-md border border-line bg-surface px-2 py-1 text-xs text-fg motion-safe:transition-opacity motion-safe:duration-[var(--dur)]"
                role="status"
              >
                <kadr-icon name="check" size="sm" />
                {{ 'landing.hero.toastClockIn' | transloco }}
              </span>
            }
          </div>
        </div>

        <!-- Scene 2: leave -->
        @if (showLeave()) {
          <div
            class="rounded-md border border-line bg-app p-3 motion-safe:transition-opacity motion-safe:duration-[var(--dur-reveal)] motion-safe:ease-[var(--ease)]"
            [class.opacity-100]="showLeave()"
          >
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div class="text-start">
                <p class="text-sm font-medium text-fg">{{ 'landing.hero.mockPending' | transloco }}</p>
                <p class="text-xs text-fg-muted">
                  {{ 'landing.hero.leaveName' | transloco }} · {{ 'landing.hero.leaveType' | transloco }}
                </p>
              </div>
              @if (!leaveApproved()) {
                <kadr-button
                  variant="primary"
                  size="sm"
                  type="button"
                  [class.scale-95]="approvePressed()"
                  (click)="noop()"
                >
                  {{ 'landing.hero.mockApprove' | transloco }}
                </kadr-button>
              } @else {
                <span class="inline-flex items-center gap-1">
                  <span
                    class="inline-flex size-5 items-center justify-center text-success motion-safe:transition-opacity motion-safe:duration-[var(--dur)]"
                    [class.opacity-100]="checkDrawn()"
                    [class.opacity-0]="!checkDrawn()"
                    aria-hidden="true"
                  >
                    <kadr-icon name="check" size="sm" />
                  </span>
                  <kadr-badge tone="success" [label]="'landing.hero.mockApproved' | transloco" />
                </span>
              }
            </div>
          </div>
        }

        <!-- Scene 3: document -->
        @if (showDoc()) {
          <div
            class="flex items-center justify-between gap-2 rounded-md border border-line bg-warning-soft px-3 py-2 text-start motion-safe:transition-opacity motion-safe:duration-[var(--dur-reveal)]"
            role="status"
          >
            <div>
              <p class="text-sm text-fg">{{ 'landing.hero.mockDocAlert' | transloco }}</p>
              <p class="text-xs text-fg-2">{{ 'landing.hero.docName' | transloco }}</p>
            </div>
            <span class="shrink-0 rounded-full bg-surface px-2 py-0.5 text-xs font-medium tabular-nums text-fg">
              {{ 'landing.hero.mockDocDays' | transloco: { n: docDays() } }}
            </span>
          </div>
        }
      </div>
    </div>
  `,
})
export class LandingProductMockComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private timers: ReturnType<typeof setTimeout>[] = [];
  private loopTimer: ReturnType<typeof setTimeout> | null = null;
  private reduceMotion = false;

  protected readonly phase = signal<ScenePhase>('clock');
  protected readonly attendanceCount = signal(18);
  protected readonly ringProgress = signal(0);
  protected readonly clockPressed = signal(false);
  protected readonly ripple = signal(false);
  protected readonly showToast = signal(false);
  protected readonly showInsidePill = signal(false);
  protected readonly showLeave = signal(false);
  protected readonly leaveApproved = signal(false);
  protected readonly approvePressed = signal(false);
  protected readonly checkDrawn = signal(false);
  protected readonly showDoc = signal(false);
  protected readonly docDays = signal(7);
  protected readonly paused = signal(false);

  ngOnInit(): void {
    this.reduceMotion =
      typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (this.reduceMotion) {
      this.showFinalFrame();
      return;
    }

    this.runLoop();

    const onVis = () => {
      if (document.visibilityState === 'hidden') {
        this.paused.set(true);
        this.clearTimers();
      } else if (this.paused()) {
        /* stay paused until user plays */
      }
    };
    document.addEventListener('visibilitychange', onVis);
    this.destroyRef.onDestroy(() => {
      document.removeEventListener('visibilitychange', onVis);
      this.clearTimers();
    });
  }

  protected ringDash(): string {
    const circ = 2 * Math.PI * 15;
    const filled = (this.ringProgress() / 100) * circ;
    return `${filled} ${circ}`;
  }

  protected togglePause(): void {
    if (this.reduceMotion) return;
    const next = !this.paused();
    this.paused.set(next);
    if (next) {
      this.clearTimers();
    } else {
      this.resetScene();
      this.runLoop();
    }
  }

  protected noop(): void {
    /* decorative */
  }

  private showFinalFrame(): void {
    this.attendanceCount.set(19);
    this.ringProgress.set(100);
    this.showToast.set(true);
    this.showInsidePill.set(true);
    this.showLeave.set(true);
    this.leaveApproved.set(true);
    this.checkDrawn.set(true);
    this.showDoc.set(true);
    this.docDays.set(7);
    this.phase.set('done');
  }

  private clearTimers(): void {
    for (const t of this.timers) clearTimeout(t);
    this.timers = [];
    if (this.loopTimer) {
      clearTimeout(this.loopTimer);
      this.loopTimer = null;
    }
  }

  private schedule(ms: number, run: () => void): void {
    this.timers.push(
      setTimeout(() => {
        if (this.paused()) return;
        run();
      }, ms),
    );
  }

  private resetScene(): void {
    this.attendanceCount.set(18);
    this.ringProgress.set(0);
    this.clockPressed.set(false);
    this.ripple.set(false);
    this.showToast.set(false);
    this.showInsidePill.set(false);
    this.showLeave.set(false);
    this.leaveApproved.set(false);
    this.approvePressed.set(false);
    this.checkDrawn.set(false);
    this.showDoc.set(false);
    this.docDays.set(7);
    this.phase.set('clock');
  }

  private runLoop(): void {
    this.clearTimers();
    this.resetScene();

    // 0–3.5s clock-in
    this.schedule(400, () => this.clockPressed.set(true));
    this.schedule(700, () => {
      this.ringProgress.set(100);
      this.ripple.set(true);
    });
    this.schedule(1100, () => {
      this.showInsidePill.set(true);
      this.showToast.set(true);
      this.attendanceCount.set(19);
      this.clockPressed.set(false);
    });
    this.schedule(2000, () => this.ripple.set(false));

    // 3.5–7.5s leave
    this.schedule(3500, () => {
      this.phase.set('leave');
      this.showLeave.set(true);
    });
    this.schedule(4800, () => this.approvePressed.set(true));
    this.schedule(5400, () => {
      this.approvePressed.set(false);
      this.leaveApproved.set(true);
      this.checkDrawn.set(true);
    });

    // 7.5–11s document
    this.schedule(7500, () => {
      this.phase.set('doc');
      this.showDoc.set(true);
      this.docDays.set(7);
    });
    this.schedule(9000, () => this.docDays.set(6));
    this.schedule(10500, () => this.phase.set('done'));

    // ~12s restart
    this.loopTimer = setTimeout(() => {
      if (!this.paused() && document.visibilityState === 'visible') {
        this.runLoop();
      }
    }, 12000);
  }
}
