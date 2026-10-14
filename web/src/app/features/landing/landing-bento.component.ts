import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { IconComponent } from '../../shared/ui/icon/icon.component';

const PAYROLL_STATUSES = [
  'landing.capabilities.payrollDraft',
  'landing.capabilities.payrollReview',
  'landing.capabilities.payrollApproved',
  'landing.capabilities.payrollLocked',
] as const;

@Component({
  selector: 'kadr-landing-bento',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, IconComponent],
  template: `
    <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-12">
      <!-- Geofencing — large -->
      <article
        class="flex flex-col rounded-lg border border-line bg-surface p-5 text-start shadow-sm sm:col-span-2 lg:col-span-7 lg:row-span-2"
      >
        <div class="mb-4 flex items-start justify-between gap-3">
          <div>
            <h3 class="text-base font-bold text-fg">{{ 'landing.capabilities.geoTitle' | transloco }}</h3>
            <p class="mt-2 text-sm text-fg-2">{{ 'landing.capabilities.geoBody' | transloco }}</p>
          </div>
          <span class="text-accent"><kadr-icon name="map-pin" size="md" /></span>
        </div>
        <div class="relative mt-auto flex min-h-36 flex-1 items-center justify-center overflow-hidden rounded-md border border-line bg-app">
          <svg class="absolute inset-0 size-full text-line opacity-50" viewBox="0 0 120 80" aria-hidden="true">
            @for (y of [10, 22, 34, 46, 58, 70]; track y) {
              @for (x of [10, 22, 34, 46, 58, 70, 82, 94, 106]; track x) {
                <circle [attr.cx]="x" [attr.cy]="y" r="1" fill="currentColor" />
              }
            }
          </svg>
          <div
            class="absolute size-24 rounded-full border border-accent/30 motion-safe:transition-transform motion-safe:duration-[var(--dur-reveal-long)]"
            [class.scale-110]="geoPulse()"
          ></div>
          <div class="absolute size-14 rounded-full border border-accent/50"></div>
          <span class="relative z-10 text-accent"><kadr-icon name="map-pin" size="lg" /></span>
          <span
            class="absolute bottom-3 start-3 rounded-full bg-success-soft px-2 py-0.5 text-xs font-medium text-success"
          >
            {{ 'landing.hero.insideGeofence' | transloco }}
          </span>
        </div>
      </article>

      <!-- Leave -->
      <article class="rounded-lg border border-line bg-surface p-5 text-start shadow-sm lg:col-span-5">
        <div class="mb-3 flex items-start justify-between gap-2">
          <div>
            <h3 class="text-base font-bold text-fg">{{ 'landing.capabilities.leaveTitle' | transloco }}</h3>
            <p class="mt-2 text-sm text-fg-2">{{ 'landing.capabilities.leaveBody' | transloco }}</p>
          </div>
          <span class="text-accent"><kadr-icon name="calendar" size="md" /></span>
        </div>
        <div class="mt-4 flex flex-wrap items-center gap-2" aria-hidden="true">
          <span class="rounded-full border border-line bg-app px-2 py-1 text-xs text-fg-2">مدير مباشر</span>
          <span class="text-fg-muted"><kadr-icon name="chevron-end" size="sm" /></span>
          <span class="rounded-full border border-line bg-app px-2 py-1 text-xs text-fg-2">الموارد البشرية</span>
          @if (leaveChecked()) {
            <span class="text-success"><kadr-icon name="check" size="sm" /></span>
          }
        </div>
      </article>

      <!-- Docs -->
      <article class="rounded-lg border border-line bg-surface p-5 text-start shadow-sm lg:col-span-5">
        <div class="mb-3 flex items-start justify-between gap-2">
          <div>
            <h3 class="text-base font-bold text-fg">{{ 'landing.capabilities.docsTitle' | transloco }}</h3>
            <p class="mt-2 text-sm text-fg-2">{{ 'landing.capabilities.docsBody' | transloco }}</p>
          </div>
          <span class="text-accent"><kadr-icon name="file" size="md" /></span>
        </div>
        <ul class="mt-3 space-y-2" aria-hidden="true">
          @for (d of docRows; track d.label) {
            <li class="flex items-center justify-between gap-2 rounded-md border border-line bg-app px-2 py-1.5 text-xs">
              <span class="text-fg">{{ d.label }}</span>
              <span class="rounded-full bg-warning-soft px-2 py-0.5 font-medium tabular-nums text-fg">{{ d.days }}</span>
            </li>
          }
        </ul>
      </article>

      <!-- ESS phone -->
      <article class="rounded-lg border border-line bg-surface p-5 text-start shadow-sm lg:col-span-4">
        <div class="mb-3 flex items-start justify-between gap-2">
          <div>
            <h3 class="text-base font-bold text-fg">{{ 'landing.capabilities.essTitle' | transloco }}</h3>
            <p class="mt-2 text-sm text-fg-2">{{ 'landing.capabilities.essBody' | transloco }}</p>
          </div>
          <span class="text-accent"><kadr-icon name="download" size="md" /></span>
        </div>
        <div class="mx-auto mt-2 w-28 rounded-lg border-2 border-line bg-app p-2" aria-hidden="true">
          <div class="h-7 rounded-md bg-accent text-center text-xs leading-7 text-on-accent">
            {{ 'landing.hero.clockIn' | transloco }}
          </div>
          <div class="mt-2 h-7 rounded-md border border-line text-center text-xs leading-7 text-fg-2">
            {{ 'landing.hero.mockPending' | transloco }}
          </div>
        </div>
      </article>

      <!-- Payroll — real F22 -->
      <article class="rounded-lg border border-line bg-surface p-5 text-start shadow-sm lg:col-span-4">
        <div class="mb-3 flex items-start justify-between gap-2">
          <div>
            <h3 class="text-base font-bold text-fg">{{ 'landing.capabilities.payrollTitle' | transloco }}</h3>
            <p class="mt-2 text-sm text-fg-2">{{ 'landing.capabilities.payrollBody' | transloco }}</p>
          </div>
          <span class="text-accent"><kadr-icon name="lock" size="md" /></span>
        </div>
        <div class="mt-4 flex flex-wrap gap-1.5" aria-hidden="true">
          @for (key of payrollKeys; track key; let i = $index) {
            <span
              class="rounded-full border px-2 py-0.5 text-xs font-medium motion-safe:transition-colors motion-safe:duration-[var(--dur)]"
              [class.border-accent]="payrollIdx() === i"
              [class.bg-accent-soft]="payrollIdx() === i"
              [class.text-accent-soft-fg]="payrollIdx() === i"
              [class.border-line]="payrollIdx() !== i"
              [class.text-fg-2]="payrollIdx() !== i"
            >
              {{ key | transloco }}
            </span>
          }
        </div>
      </article>

      <!-- Core HR -->
      <article class="rounded-lg border border-line bg-surface p-5 text-start shadow-sm lg:col-span-4">
        <div class="mb-3 flex items-start justify-between gap-2">
          <div>
            <h3 class="text-base font-bold text-fg">{{ 'landing.capabilities.hrTitle' | transloco }}</h3>
            <p class="mt-2 text-sm text-fg-2">{{ 'landing.capabilities.hrBody' | transloco }}</p>
          </div>
          <span class="text-accent"><kadr-icon name="users" size="md" /></span>
        </div>
        <ul class="mt-3 space-y-1.5 text-xs" aria-hidden="true">
          <li class="flex items-center justify-between rounded-md border border-line bg-app px-2 py-1.5">
            <span class="text-fg">{{ 'landing.capabilities.deptOps' | transloco }}</span>
            <span class="tabular-nums text-fg-2">{{ 'landing.capabilities.headcount' | transloco: { n: 12 } }}</span>
          </li>
          <li class="ms-4 flex items-center justify-between rounded-md border border-line bg-app px-2 py-1.5">
            <span class="text-fg">{{ 'landing.capabilities.deptHr' | transloco }}</span>
            <span class="tabular-nums text-fg-2">{{ 'landing.capabilities.headcount' | transloco: { n: 3 } }}</span>
          </li>
        </ul>
      </article>
    </div>
  `,
})
export class LandingBentoComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  protected readonly payrollKeys = PAYROLL_STATUSES;
  protected readonly payrollIdx = signal(0);
  protected readonly leaveChecked = signal(false);
  protected readonly geoPulse = signal(false);
  protected readonly docRows = [
    { label: 'بطاقة شخصية', days: '60' },
    { label: 'رخصة', days: '30' },
    { label: 'عقد', days: '7' },
  ];

  ngOnInit(): void {
    const reduce =
      typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduce) {
      this.leaveChecked.set(true);
      this.payrollIdx.set(3);
      this.geoPulse.set(true);
      return;
    }

    let leaveT = 0;
    let payT = 0;
    let geoT = 0;
    const start = () => {
      leaveT = window.setInterval(() => this.leaveChecked.update((v) => !v), 2400);
      payT = window.setInterval(
        () => this.payrollIdx.update((i) => (i + 1) % PAYROLL_STATUSES.length),
        1800,
      );
      geoT = window.setInterval(() => this.geoPulse.update((v) => !v), 2000);
    };
    const stop = () => {
      clearInterval(leaveT);
      clearInterval(payT);
      clearInterval(geoT);
    };
    start();
    const onVis = () => {
      if (document.visibilityState === 'hidden') stop();
      else start();
    };
    document.addEventListener('visibilitychange', onVis);
    this.destroyRef.onDestroy(() => {
      stop();
      document.removeEventListener('visibilitychange', onVis);
    });
  }
}
