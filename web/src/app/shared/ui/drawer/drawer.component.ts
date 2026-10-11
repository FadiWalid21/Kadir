import { ChangeDetectionStrategy, Component, effect, input, output } from '@angular/core';

/** Side panel drawer (overlay + end panel). Uses logical inset for RTL. */
@Component({
  selector: 'kadr-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="fixed inset-0 z-50 flex justify-end" role="presentation">
        <button
          type="button"
          class="absolute inset-0 bg-fg/40"
          [attr.aria-label]="closeLabel()"
          (click)="requestClose()"
        ></button>
        <aside
          class="relative z-10 flex h-full w-full flex-col border-s border-line bg-surface shadow-lg"
          [class.max-w-md]="size() === 'md'"
          [class.max-w-lg]="size() === 'lg'"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
          [attr.aria-describedby]="subtitle() ? subtitleId : null"
        >
          <header class="flex items-start justify-between gap-3 border-b border-line px-4 py-3">
            <div class="min-w-0 flex-1 text-start">
              <h2 [id]="titleId" class="text-lg font-bold text-fg">{{ title() }}</h2>
              @if (subtitle()) {
                <p [id]="subtitleId" class="mt-1 text-sm text-fg-2">{{ subtitle() }}</p>
              }
            </div>
            <button
              type="button"
              class="inline-flex size-9 shrink-0 items-center justify-center rounded-md text-fg-2 hover:bg-subtle focus-visible:outline-none"
              [attr.aria-label]="closeLabel()"
              (click)="requestClose()"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="size-5" aria-hidden="true">
                <path d="M18 6 6 18" />
                <path d="m6 6 12 12" />
              </svg>
            </button>
          </header>
          <div class="min-h-0 flex-1 overflow-y-auto px-4 py-4">
            <ng-content />
          </div>
          @if (hasFooter()) {
            <footer class="flex flex-wrap gap-2 border-t border-line px-4 py-3">
              <ng-content select="[drawerFooter]" />
            </footer>
          }
        </aside>
      </div>
    }
  `,
})
export class DrawerComponent {
  readonly open = input(false);
  readonly title = input.required<string>();
  readonly subtitle = input<string | undefined>(undefined);
  readonly closeLabel = input.required<string>();
  readonly hasFooter = input(true);
  /** md ≈ 28rem, lg ≈ 32rem (invite drawer). */
  readonly size = input<'md' | 'lg'>('md');
  readonly closed = output<void>();

  protected readonly titleId = `drawer-title-${Math.random().toString(36).slice(2, 9)}`;
  protected readonly subtitleId = `drawer-sub-${Math.random().toString(36).slice(2, 9)}`;

  constructor() {
    effect(() => {
      if (typeof document === 'undefined') {
        return;
      }
      document.body.style.overflow = this.open() ? 'hidden' : '';
    });
  }

  requestClose(): void {
    this.closed.emit();
  }
}
