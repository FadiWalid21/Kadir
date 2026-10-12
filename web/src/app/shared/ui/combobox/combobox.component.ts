import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { IconComponent } from '../icon/icon.component';
import { SkeletonComponent } from '../skeleton/skeleton.component';

export interface ComboboxOption {
  id: string;
  name: string;
}

export type ComboboxSearchFn = (q: string) => Promise<ComboboxOption[]>;

let nextComboboxId = 0;

@Component({
  selector: 'kadr-combobox',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CdkConnectedOverlay, CdkOverlayOrigin, TranslocoPipe, IconComponent, SkeletonComponent],
  template: `
    <div class="grid w-full gap-2">
      <label [attr.for]="controlId" class="text-sm font-medium text-fg">{{ label() }}</label>
      <div class="relative" cdkOverlayOrigin #origin="cdkOverlayOrigin">
        <input
          [id]="controlId"
          type="text"
          role="combobox"
          autocomplete="off"
          class="h-control w-full rounded-md border border-line-strong bg-surface pe-10 ps-3 text-base text-fg focus-visible:outline-none disabled:opacity-50"
          [attr.aria-expanded]="open()"
          [attr.aria-controls]="listboxId"
          [attr.aria-activedescendant]="activeOptionId()"
          aria-autocomplete="list"
          [attr.aria-invalid]="!!error() || null"
          [attr.aria-describedby]="describedBy"
          [disabled]="disabled()"
          [placeholder]="placeholder() ?? ''"
          [value]="inputText()"
          (focus)="onFocus()"
          (input)="onInput($event)"
          (keydown)="onKeydown($event)"
        />
        <span class="pointer-events-none absolute end-0 top-0 flex size-11 items-center justify-center text-fg-muted">
          <kadr-icon name="chevron-down" size="sm" />
        </span>
      </div>
      @if (error()) {
        <p [id]="errorId" class="text-xs text-danger" role="alert">{{ error() }}</p>
      } @else if (hint()) {
        <p [id]="hintId" class="text-xs text-fg-muted">{{ hint() }}</p>
      }

      <ng-template
        cdkConnectedOverlay
        [cdkConnectedOverlayOrigin]="origin"
        [cdkConnectedOverlayOpen]="open()"
        [cdkConnectedOverlayHasBackdrop]="true"
        cdkConnectedOverlayBackdropClass="cdk-overlay-transparent-backdrop"
        [cdkConnectedOverlayPositions]="positions"
        [cdkConnectedOverlayWidth]="'auto'"
        [cdkConnectedOverlayMinWidth]="originWidth()"
        (backdropClick)="close()"
        (detach)="close()"
      >
        <div
          [id]="listboxId"
          role="listbox"
          class="max-h-60 overflow-y-auto rounded-md border border-line bg-surface py-1 shadow-md"
          [style.min-width.px]="originWidth()"
        >
          @if (state() === 'loading') {
            <div class="flex flex-col gap-2 px-3 py-2" role="status">
              <kadr-skeleton height="1.5rem" />
              <kadr-skeleton height="1.5rem" />
            </div>
          } @else if (state() === 'error') {
            <p class="px-3 py-2 text-sm text-danger">{{ 'ui.combobox.error' | transloco }}</p>
          } @else if (options().length === 0) {
            <p class="px-3 py-2 text-sm text-fg-muted">{{ 'ui.combobox.empty' | transloco }}</p>
          } @else {
            @for (opt of options(); track opt.id; let i = $index) {
              <button
                type="button"
                role="option"
                class="flex w-full px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
                [id]="optionId(i)"
                [attr.aria-selected]="activeIndex() === i"
                [class.bg-accent-soft]="activeIndex() === i"
                [class.text-accent-soft-fg]="activeIndex() === i"
                (click)="select(opt)"
                (mouseenter)="activeIndex.set(i)"
              >
                {{ opt.name }}
              </button>
            }
          }
        </div>
      </ng-template>
    </div>
  `,
})
export class ComboboxComponent {
  readonly label = input.required<string>();
  readonly searchFn = input.required<ComboboxSearchFn>();
  readonly value = input<string>('');
  readonly displayName = input<string>('');
  readonly placeholder = input<string | undefined>(undefined);
  readonly hint = input<string | undefined>(undefined);
  readonly error = input<string | undefined>(undefined);
  readonly disabled = input(false);
  readonly debounceMs = input(250);

  readonly valueChange = output<string>();
  readonly selectionChange = output<ComboboxOption | null>();

  protected readonly open = signal(false);
  protected readonly inputText = signal('');
  protected readonly options = signal<ComboboxOption[]>([]);
  protected readonly state = signal<'idle' | 'loading' | 'ready' | 'error'>('idle');
  protected readonly activeIndex = signal(-1);
  protected readonly originWidth = signal(280);

  protected readonly controlId = `kadr-combobox-${++nextComboboxId}`;
  protected readonly listboxId = `${this.controlId}-listbox`;
  protected readonly hintId = `${this.controlId}-hint`;
  protected readonly errorId = `${this.controlId}-error`;

  protected readonly positions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
  ];

  private debounceHandle: ReturnType<typeof setTimeout> | null = null;
  private searchSeq = 0;
  private skipNextSync = false;

  protected readonly activeOptionId = computed(() => {
    const i = this.activeIndex();
    return i >= 0 ? this.optionId(i) : null;
  });

  protected get describedBy(): string | null {
    if (this.error()) {
      return this.errorId;
    }
    if (this.hint()) {
      return this.hintId;
    }
    return null;
  }

  constructor() {
    effect(() => {
      const name = this.displayName();
      const id = this.value();
      if (this.skipNextSync) {
        this.skipNextSync = false;
        return;
      }
      if (name) {
        this.inputText.set(name);
      } else if (!id) {
        this.inputText.set('');
      }
    });

    // When the search function identity changes (e.g. target type switch), drop stale options.
    effect(() => {
      this.searchFn();
      this.resetOptions();
    });
  }

  /** Clears cached options so the next open fetches with the current searchFn. */
  reset(): void {
    this.resetOptions();
    this.skipNextSync = true;
    this.inputText.set('');
    this.close();
  }

  private resetOptions(): void {
    this.options.set([]);
    this.activeIndex.set(-1);
    this.state.set('idle');
    this.searchSeq++;
  }

  protected optionId(index: number): string {
    return `${this.listboxId}-opt-${index}`;
  }

  protected onFocus(): void {
    if (this.disabled()) {
      return;
    }
    this.openPanel();
  }

  protected onInput(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.inputText.set(text);
    if (this.value()) {
      this.skipNextSync = true;
      this.valueChange.emit('');
      this.selectionChange.emit(null);
    }
    this.openPanel();
    this.scheduleSearch(text);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (this.disabled()) {
      return;
    }
    const opts = this.options();
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!this.open()) {
          this.openPanel();
        } else if (opts.length) {
          this.activeIndex.set(Math.min(opts.length - 1, this.activeIndex() + 1));
        }
        break;
      case 'ArrowUp':
        event.preventDefault();
        if (opts.length) {
          this.activeIndex.set(Math.max(0, this.activeIndex() - 1));
        }
        break;
      case 'Home':
        if (this.open() && opts.length) {
          event.preventDefault();
          this.activeIndex.set(0);
        }
        break;
      case 'End':
        if (this.open() && opts.length) {
          event.preventDefault();
          this.activeIndex.set(opts.length - 1);
        }
        break;
      case 'Enter':
        if (this.open() && this.activeIndex() >= 0 && opts[this.activeIndex()]) {
          event.preventDefault();
          this.select(opts[this.activeIndex()]!);
        }
        break;
      case 'Escape':
        if (this.open()) {
          event.preventDefault();
          this.close();
        }
        break;
      default:
        break;
    }
  }

  protected select(opt: ComboboxOption): void {
    this.skipNextSync = true;
    this.inputText.set(opt.name);
    this.valueChange.emit(opt.id);
    this.selectionChange.emit(opt);
    this.close();
  }

  protected close(): void {
    this.open.set(false);
    this.activeIndex.set(-1);
  }

  private openPanel(): void {
    this.open.set(true);
    const el = document.getElementById(this.controlId);
    if (el) {
      this.originWidth.set(Math.max(el.getBoundingClientRect().width, 200));
    }
    // Always refresh so a prior search for another dataset cannot linger.
    this.scheduleSearch(this.inputText());
  }

  private scheduleSearch(q: string): void {
    if (this.debounceHandle) {
      clearTimeout(this.debounceHandle);
    }
    this.debounceHandle = setTimeout(() => {
      void this.runSearch(q);
    }, this.debounceMs());
  }

  private async runSearch(q: string): Promise<void> {
    const seq = ++this.searchSeq;
    this.state.set('loading');
    try {
      const results = await this.searchFn()(q);
      if (seq !== this.searchSeq) {
        return;
      }
      this.options.set(results);
      this.activeIndex.set(results.length ? 0 : -1);
      this.state.set('ready');
    } catch {
      if (seq !== this.searchSeq) {
        return;
      }
      this.options.set([]);
      this.activeIndex.set(-1);
      this.state.set('error');
    }
  }
}
