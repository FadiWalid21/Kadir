import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'kadr-landing-how',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, IconComponent],
  template: `
    <div class="grid gap-8 lg:grid-cols-2 lg:items-start">
      <ol class="relative flex flex-col text-start">
        <div class="pointer-events-none absolute start-[0.85rem] top-3 bottom-3 w-px bg-subtle" aria-hidden="true">
          <div
            class="w-full origin-top bg-accent transition-transform duration-[var(--dur-reveal)] ease-[var(--ease)] motion-reduce:scale-y-100"
            [style.height]="'100%'"
            [style.transform]="'scaleY(' + progress() + ')'"
          ></div>
        </div>
        @for (step of steps; track step.id; let i = $index) {
          <li class="relative ps-10 pb-8 last:pb-0">
            <button
              type="button"
              class="w-full rounded-md p-3 text-start transition-colors focus-visible:outline-none hover:bg-hover"
              [class.bg-accent-soft]="active() === i"
              [attr.aria-current]="active() === i ? 'step' : null"
              (click)="stepSelect.emit(i)"
            >
              <span
                class="absolute start-0 top-4 flex size-7 items-center justify-center rounded-full border text-xs font-bold"
                [class.border-accent]="active() === i"
                [class.bg-accent]="active() === i"
                [class.text-on-accent]="active() === i"
                [class.border-line]="active() !== i"
                [class.bg-surface]="active() !== i"
                [class.text-fg-2]="active() !== i"
              >
                {{ i + 1 }}
              </span>
              <h3 class="text-base font-bold text-fg">{{ step.titleKey | transloco }}</h3>
              <p class="mt-1 text-sm text-fg-2">{{ step.bodyKey | transloco }}</p>
            </button>
            <div class="mt-3 lg:hidden">
              <div class="rounded-md border border-line bg-app p-4">
                @if (i === 0) {
                  @for (row of csvRows; track row.name; let ri = $index) {
                    <div class="mb-2 flex items-center justify-between gap-2 rounded-md border border-line bg-surface px-2 py-2 text-xs last:mb-0">
                      <span class="truncate text-fg">{{ row.name }}</span>
                      <span class="truncate text-fg-2">{{ row.title }}</span>
                      @if (csvVisible() > ri) {
                        <span class="text-success"><kadr-icon name="check" size="sm" /></span>
                      }
                    </div>
                  }
                } @else if (i === 1) {
                  <div class="relative mx-auto flex h-36 w-full items-center justify-center">
                    <div class="absolute size-28 rounded-full border border-accent/25"></div>
                    <div class="absolute size-16 rounded-full border border-accent/40"></div>
                    <span class="text-accent"><kadr-icon name="map-pin" size="lg" /></span>
                  </div>
                } @else {
                  <div class="mx-auto w-32 rounded-lg border-2 border-line bg-surface p-3">
                    <div class="h-8 rounded-md bg-accent text-center text-xs leading-8 text-on-accent">
                      {{ 'landing.hero.clockIn' | transloco }}
                    </div>
                  </div>
                }
              </div>
            </div>
          </li>
        }
      </ol>

      <div class="hidden lg:block">
        <div class="sticky top-24 min-h-64 rounded-lg border border-line bg-app p-5 shadow-sm">
          @switch (active()) {
            @case (0) {
              <div class="space-y-2 text-start" aria-hidden="true">
                <div class="grid grid-cols-3 gap-2 text-xs font-medium text-fg-muted">
                  <span>{{ 'landing.how.colName' | transloco }}</span>
                  <span>{{ 'landing.how.colTitle' | transloco }}</span>
                  <span>{{ 'landing.how.colBranch' | transloco }}</span>
                </div>
                @for (row of csvRows; track row.name; let ri = $index) {
                  <div
                    class="grid grid-cols-3 gap-2 rounded-md border border-line bg-surface px-2 py-2 text-xs text-fg motion-safe:transition-opacity motion-safe:duration-[var(--dur)]"
                    [class.opacity-100]="csvVisible() > ri"
                    [class.opacity-40]="csvVisible() <= ri"
                  >
                    <span class="truncate">{{ row.name }}</span>
                    <span class="truncate text-fg-2">{{ row.title }}</span>
                    <span class="inline-flex items-center justify-between gap-1 truncate text-fg-2">
                      {{ row.branch }}
                      @if (csvVisible() > ri) {
                        <span class="text-success"><kadr-icon name="check" size="sm" /></span>
                      }
                    </span>
                  </div>
                }
              </div>
            }
            @case (1) {
              <div class="relative mx-auto flex h-48 w-full max-w-xs items-center justify-center" aria-hidden="true">
                <div
                  class="absolute size-36 rounded-full border border-accent/30 motion-safe:transition-transform motion-safe:duration-[var(--dur-reveal-long)]"
                  [class.scale-100]="geoRing()"
                  [class.scale-75]="!geoRing()"
                ></div>
                <div
                  class="absolute size-20 rounded-full border border-accent/50 motion-safe:transition-transform motion-safe:duration-[var(--dur-reveal)]"
                  [class.scale-100]="geoRing()"
                  [class.scale-50]="!geoRing()"
                ></div>
                <span class="relative z-10 text-accent"><kadr-icon name="map-pin" size="lg" /></span>
                <div class="absolute bottom-2 start-0 end-0 flex flex-wrap justify-center gap-1">
                  @for (chip of geoChips(); track chip) {
                    <span class="rounded-full bg-accent-soft px-2 py-0.5 text-xs font-medium text-accent-soft-fg">
                      {{ chip }}
                    </span>
                  }
                </div>
              </div>
            }
            @default {
              <div class="flex flex-col items-center gap-4" aria-hidden="true">
                <p
                  class="inline-flex items-center gap-2 rounded-md border border-line bg-surface px-3 py-2 text-sm text-fg motion-safe:transition-opacity motion-safe:duration-[var(--dur)]"
                  [class.opacity-100]="inviteSent()"
                  [class.opacity-40]="!inviteSent()"
                >
                  <span class="text-success"><kadr-icon name="check" size="sm" /></span>
                  {{ 'landing.how.inviteSent' | transloco }}
                </p>
                <div class="w-36 rounded-lg border-2 border-line bg-surface p-3 shadow-sm">
                  <p class="text-center text-xs text-fg-muted">{{ 'landing.how.installedApp' | transloco }}</p>
                  <div class="mt-3 space-y-2">
                    <div class="h-8 rounded-md bg-accent text-center text-xs leading-8 text-on-accent">
                      {{ 'landing.hero.clockIn' | transloco }}
                    </div>
                    <div class="h-8 rounded-md border border-line text-center text-xs leading-8 text-fg-2">
                      {{ 'landing.hero.mockPending' | transloco }}
                    </div>
                  </div>
                </div>
              </div>
            }
          }
        </div>
      </div>
    </div>
  `,
})
export class LandingHowComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  readonly active = input.required<number>();
  readonly progress = input.required<number>();
  readonly stepSelect = output<number>();

  protected readonly steps = [
    { id: 1, titleKey: 'landing.how.step1Title', bodyKey: 'landing.how.step1Body' },
    { id: 2, titleKey: 'landing.how.step2Title', bodyKey: 'landing.how.step2Body' },
    { id: 3, titleKey: 'landing.how.step3Title', bodyKey: 'landing.how.step3Body' },
  ];

  protected readonly csvRows = [
    { name: 'نورة علي', title: 'محاسبة', branch: 'الفرع الرئيسي' },
    { name: 'كريم فؤاد', title: 'بائع', branch: 'الفرع 2' },
    { name: 'ليلى حسن', title: 'مشرفة', branch: 'الفرع 1' },
  ];

  protected readonly csvVisible = signal(0);
  protected readonly geoRing = signal(false);
  protected readonly geoChips = signal<string[]>([]);
  protected readonly inviteSent = signal(false);
  private timers: ReturnType<typeof setTimeout>[] = [];

  constructor() {
    effect(() => {
      const a = this.active();
      this.playStage(a);
    });
  }

  ngOnInit(): void {
    const reduce =
      typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduce) {
      this.csvVisible.set(3);
      this.geoRing.set(true);
      this.geoChips.set(['صباحية', 'سنوية']);
      this.inviteSent.set(true);
    }
    this.destroyRef.onDestroy(() => this.clearTimers());
  }

  private clearTimers(): void {
    for (const t of this.timers) clearTimeout(t);
    this.timers = [];
  }

  private playStage(i: number): void {
    const reduce =
      typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduce) return;
    this.clearTimers();
    if (i === 0) {
      this.csvVisible.set(0);
      this.timers.push(setTimeout(() => this.csvVisible.set(1), 200));
      this.timers.push(setTimeout(() => this.csvVisible.set(2), 500));
      this.timers.push(setTimeout(() => this.csvVisible.set(3), 800));
    } else if (i === 1) {
      this.geoRing.set(false);
      this.geoChips.set([]);
      this.timers.push(setTimeout(() => this.geoRing.set(true), 100));
      this.timers.push(setTimeout(() => this.geoChips.set(['صباحية']), 400));
      this.timers.push(setTimeout(() => this.geoChips.set(['صباحية', 'سنوية']), 700));
    } else {
      this.inviteSent.set(false);
      this.timers.push(setTimeout(() => this.inviteSent.set(true), 300));
    }
  }
}
