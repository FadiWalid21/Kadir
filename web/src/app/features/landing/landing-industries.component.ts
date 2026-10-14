import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { toSignal } from '@angular/core/rxjs-interop';
import { startWith } from 'rxjs';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import {
  IndustryCode,
  LANDING_INDUSTRY_SEEDS,
} from './landing-industry-seeds';

@Component({
  selector: 'kadr-landing-industries',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslocoPipe, ButtonComponent, IconComponent],
  template: `
    <div
      class="flex flex-wrap gap-1 border-b border-line"
      role="tablist"
      [attr.aria-label]="'landing.industries.title' | transloco"
    >
      @for (seed of seeds; track seed.code) {
        <button
          type="button"
          role="tab"
          class="inline-flex items-center gap-2 border-b-2 px-3 py-2 text-sm font-medium transition-colors focus-visible:outline-none"
          [class.border-accent]="activeId() === seed.code"
          [class.text-fg]="activeId() === seed.code"
          [class.border-transparent]="activeId() !== seed.code"
          [class.text-fg-2]="activeId() !== seed.code"
          [attr.aria-selected]="activeId() === seed.code"
          [attr.id]="'industry-tab-' + seed.code"
          [attr.aria-controls]="'industry-panel-' + seed.code"
          (click)="select(seed.code)"
          (keydown)="onKey($event, seed.code)"
        >
          <kadr-icon [name]="seed.icon" size="sm" />
          <span>{{ isEn() ? seed.nameEn : seed.nameAr }}</span>
        </button>
      }
    </div>

    @if (active(); as ind) {
      <div
        class="mt-5 rounded-lg border border-line bg-app p-5 text-start motion-safe:transition-opacity motion-safe:duration-[var(--dur)]"
        role="tabpanel"
        [attr.id]="'industry-panel-' + ind.code"
        [attr.aria-labelledby]="'industry-tab-' + ind.code"
        [class.opacity-100]="panelReady()"
        [class.opacity-0]="!panelReady()"
      >
        <div class="space-y-5">
          <div>
            <p class="text-xs font-medium text-fg-muted">{{ 'landing.industries.shifts' | transloco }}</p>
            <div class="mt-2 space-y-2">
              @for (s of ind.shifts; track s.start + s.nameEn) {
                <div class="flex items-center gap-3">
                  <span class="w-24 shrink-0 truncate text-xs text-fg">{{ isEn() ? s.nameEn : s.nameAr }}</span>
                  <div class="relative h-2 flex-1 overflow-hidden rounded-full bg-subtle">
                    <div
                      class="absolute inset-y-0 rounded-full bg-accent/70"
                      [style.inset-inline-start.%]="shiftStartPct(s.start)"
                      [style.width.%]="shiftWidthPct(s.start, s.end)"
                    ></div>
                  </div>
                  <span class="shrink-0 text-xs tabular-nums text-fg-2" dir="ltr">{{ s.start }}–{{ s.end }}</span>
                </div>
              }
            </div>
          </div>

          <div>
            <p class="text-xs font-medium text-fg-muted">{{ 'landing.industries.leaveTypes' | transloco }}</p>
            <div class="mt-2 flex flex-wrap gap-1.5">
              @for (l of ind.leaveTypes; track l.nameEn) {
                <span class="rounded-full border border-line bg-surface px-2 py-0.5 text-xs text-fg">
                  {{ isEn() ? l.nameEn : l.nameAr }}
                </span>
              }
            </div>
          </div>

          <div>
            <p class="text-xs font-medium text-fg-muted">{{ 'landing.industries.documents' | transloco }}</p>
            <ul class="mt-2 space-y-1.5">
              @for (d of ind.documentTypes; track d.nameEn) {
                <li class="flex flex-wrap items-center gap-2 text-xs">
                  <span class="text-fg">{{ isEn() ? d.nameEn : d.nameAr }}</span>
                  @if (d.hasExpiry) {
                    @for (day of d.alertDays; track day) {
                      <span class="rounded-full bg-warning-soft px-1.5 py-0.5 tabular-nums text-fg">{{ day }}</span>
                    }
                  }
                </li>
              }
            </ul>
          </div>

          <div>
            <p class="text-xs font-medium text-fg-muted">{{ 'landing.industries.jobTitles' | transloco }}</p>
            <div class="mt-2 flex flex-wrap gap-1.5">
              @for (j of ind.jobTitles; track j.nameEn) {
                <span class="rounded-md border border-line bg-surface px-2 py-1 text-xs text-fg-2">
                  {{ isEn() ? j.nameEn : j.nameAr }}
                </span>
              }
            </div>
          </div>

          <div class="flex flex-wrap items-center gap-3 border-t border-line pt-4">
            <p class="text-sm text-fg-2">{{ isEn() ? ind.ctaLineEn : ind.ctaLineAr }}</p>
            <a routerLink="/register" class="inline-flex">
              <kadr-button variant="primary" type="button" size="sm">
                {{ 'landing.cta.primary' | transloco }}
              </kadr-button>
            </a>
          </div>
        </div>
      </div>
    }
  `,
})
export class LandingIndustriesComponent {
  private readonly transloco = inject(TranslocoService);
  private readonly lang = toSignal(this.transloco.langChanges$.pipe(startWith(this.transloco.getActiveLang())), {
    initialValue: this.transloco.getActiveLang(),
  });

  protected readonly seeds = LANDING_INDUSTRY_SEEDS;
  protected readonly activeId = signal<IndustryCode>('retail');
  protected readonly panelReady = signal(true);

  protected readonly isEn = computed(() => this.lang() === 'en');
  protected readonly active = computed(() => this.seeds.find((s) => s.code === this.activeId()) ?? this.seeds[0]);

  protected select(code: IndustryCode): void {
    if (code === this.activeId()) return;
    this.panelReady.set(false);
    this.activeId.set(code);
    requestAnimationFrame(() => this.panelReady.set(true));
  }

  protected onKey(event: KeyboardEvent, code: IndustryCode): void {
    const idx = this.seeds.findIndex((s) => s.code === code);
    if (idx < 0) return;
    if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
      event.preventDefault();
      const dir = event.key === 'ArrowRight' ? 1 : -1;
      const rtl = document.documentElement.dir === 'rtl';
      const delta = rtl ? -dir : dir;
      const next = (idx + delta + this.seeds.length) % this.seeds.length;
      const nextCode = this.seeds[next]!.code;
      this.select(nextCode);
      document.getElementById(`industry-tab-${nextCode}`)?.focus();
    }
  }

  protected shiftStartPct(start: string): number {
    return (this.toMinutes(start) / (24 * 60)) * 100;
  }

  protected shiftWidthPct(start: string, end: string): number {
    const a = this.toMinutes(start);
    let b = this.toMinutes(end);
    if (b <= a) b += 24 * 60;
    return Math.max(8, ((b - a) / (24 * 60)) * 100);
  }

  private toMinutes(hhmm: string): number {
    const [h, m] = hhmm.split(':').map(Number);
    return (h ?? 0) * 60 + (m ?? 0);
  }
}
