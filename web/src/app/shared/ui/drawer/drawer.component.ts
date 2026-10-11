import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';

const MOBILE_MQ = '(max-width: 639px)';

/** Side panel on md+; bottom sheet below 640px (F13 / UX-08). */
@Component({
  selector: 'kadr-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div
        class="fixed inset-0 z-50 flex"
        [class.items-end]="sheet()"
        [class.justify-center]="sheet()"
        [class.justify-end]="!sheet()"
        role="presentation"
        data-testid="drawer-root"
        [attr.data-mode]="sheet() ? 'sheet' : 'panel'"
      >
        <button
          type="button"
          class="absolute inset-0 bg-fg/40"
          [attr.aria-label]="closeLabel()"
          (click)="requestClose()"
        ></button>
        <aside
          #panel
          class="relative z-10 flex w-full flex-col border-line bg-surface shadow-lg outline-none"
          [class.h-full]="!sheet()"
          [class.border-s]="!sheet()"
          [class.max-w-md]="!sheet() && size() === 'md'"
          [class.max-w-lg]="!sheet() && size() === 'lg'"
          [class.kadr-drawer-sheet]="sheet()"
          [class.rounded-t-lg]="sheet()"
          [class.border-t]="sheet()"
          role="dialog"
          aria-modal="true"
          tabindex="-1"
          [attr.aria-labelledby]="titleId"
          [attr.aria-describedby]="subtitle() ? subtitleId : null"
        >
          @if (sheet()) {
            <div class="flex justify-center pt-2" aria-hidden="true">
              <span class="h-1 w-10 rounded-full bg-line-strong"></span>
            </div>
          }
          <header class="flex items-start justify-between gap-3 border-b border-line px-4 py-3">
            <div class="min-w-0 flex-1 text-start">
              <h2 [id]="titleId" class="text-lg font-bold text-fg">{{ title() }}</h2>
              @if (subtitle()) {
                <p [id]="subtitleId" class="mt-1 text-sm text-fg-2">{{ subtitle() }}</p>
              }
            </div>
            <button
              type="button"
              class="inline-flex size-11 shrink-0 items-center justify-center rounded-md text-fg-2 hover:bg-subtle focus-visible:outline-none"
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
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly panelRef = viewChild<ElementRef<HTMLElement>>('panel');

  readonly open = input(false);
  readonly title = input.required<string>();
  readonly subtitle = input<string | undefined>(undefined);
  readonly closeLabel = input.required<string>();
  readonly hasFooter = input(true);
  readonly size = input<'md' | 'lg'>('md');
  readonly closed = output<void>();

  protected readonly sheet = signal(false);
  protected readonly titleId = `drawer-title-${Math.random().toString(36).slice(2, 9)}`;
  protected readonly subtitleId = `drawer-sub-${Math.random().toString(36).slice(2, 9)}`;

  private previousFocus: HTMLElement | null = null;
  private media?: MediaQueryList;

  constructor() {
    if (typeof matchMedia === 'function') {
      this.media = matchMedia(MOBILE_MQ);
      this.sheet.set(this.media.matches);
      const onChange = () => this.sheet.set(!!this.media?.matches);
      this.media.addEventListener('change', onChange);
      this.destroyRef.onDestroy(() => this.media?.removeEventListener('change', onChange));
    }

    effect(() => {
      if (typeof document === 'undefined') {
        return;
      }
      const isOpen = this.open();
      this.document.body.style.overflow = isOpen ? 'hidden' : '';
      if (isOpen) {
        this.previousFocus = this.document.activeElement as HTMLElement | null;
        queueMicrotask(() => this.panelRef()?.nativeElement.focus());
      } else if (this.previousFocus) {
        this.previousFocus.focus();
        this.previousFocus = null;
      }
    });

    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && this.open()) {
        event.preventDefault();
        this.requestClose();
      }
    };
    this.document.addEventListener('keydown', onKey);
    this.destroyRef.onDestroy(() => {
      this.document.removeEventListener('keydown', onKey);
      this.document.body.style.overflow = '';
    });
  }

  requestClose(): void {
    this.closed.emit();
  }
}
