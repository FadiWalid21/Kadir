import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { GuidedTourService } from '../../../core/tour/guided-tour.service';
import { ButtonComponent } from '../button/button.component';

@Component({
  selector: 'kadr-guided-tour',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent],
  template: `
    @if (tour.active() && tour.current(); as step) {
      <div
        class="fixed inset-0 z-[60]"
        role="dialog"
        aria-modal="true"
        [attr.aria-labelledby]="titleId"
        data-testid="guided-tour"
      >
        <button
          type="button"
          class="absolute inset-0 bg-fg/50"
          [attr.aria-label]="'tour.skip' | transloco"
          (click)="tour.skip()"
        ></button>
        @if (hole(); as h) {
          <div
            class="pointer-events-none absolute rounded-md ring-2 ring-accent ring-offset-2 ring-offset-transparent"
            [style.top.px]="h.top"
            [style.left.px]="h.left"
            [style.width.px]="h.width"
            [style.height.px]="h.height"
          ></div>
        }
        <div
          class="absolute z-10 w-[min(22rem,calc(100vw-2rem))] rounded-md border border-line bg-surface p-4 shadow-md"
          [style.top.px]="cardTop()"
          [style.inset-inline-start.px]="cardStart()"
        >
          <p class="text-xs tabular-nums text-fg-muted">
            {{ 'tour.progress' | transloco: { current: tour.index() + 1, total: tour.steps().length } }}
          </p>
          <h2 [id]="titleId" class="mt-1 text-base font-bold text-fg">
            {{ step.titleKey | transloco }}
          </h2>
          <p class="mt-2 text-sm text-fg-2">{{ step.bodyKey | transloco }}</p>
          <div class="mt-4 flex flex-wrap gap-2">
            <kadr-button variant="primary" size="sm" type="button" (click)="tour.next()">
              {{ (tour.isLast() ? 'tour.done' : 'tour.next') | transloco }}
            </kadr-button>
            <kadr-button variant="ghost" size="sm" type="button" (click)="tour.skip()">
              {{ 'tour.skip' | transloco }}
            </kadr-button>
          </div>
        </div>
      </div>
    }
  `,
})
export class GuidedTourComponent {
  protected readonly tour = inject(GuidedTourService);
  private readonly document = inject(DOCUMENT);

  protected readonly titleId = 'kadr-guided-tour-title';
  protected readonly hole = signal<{ top: number; left: number; width: number; height: number } | null>(
    null,
  );
  protected readonly cardTop = signal(96);
  protected readonly cardStart = signal(16);

  private readonly layout = effect(() => {
    const step = this.tour.current();
    const active = this.tour.active();
    if (!active || !step) {
      this.hole.set(null);
      return;
    }
    queueMicrotask(() => this.position(step));
  });

  private position(step: { target: string; mobileTarget?: string }): void {
    const selector = this.tour.resolveTargetSelector(step);
    const el = this.document.querySelector(selector) as HTMLElement | null;
    if (!el) {
      this.hole.set(null);
      this.cardTop.set(96);
      this.cardStart.set(16);
      return;
    }
    const rect = el.getBoundingClientRect();
    const pad = 4;
    this.hole.set({
      top: rect.top - pad,
      left: rect.left - pad,
      width: rect.width + pad * 2,
      height: rect.height + pad * 2,
    });
    const below = rect.bottom + 12;
    const above = rect.top - 160;
    const top = below + 180 < (this.document.defaultView?.innerHeight ?? 800) ? below : Math.max(16, above);
    const start = Math.min(
      Math.max(16, rect.left),
      (this.document.defaultView?.innerWidth ?? 400) - 16 - 352,
    );
    this.cardTop.set(top);
    this.cardStart.set(Math.max(16, start));
  }
}
