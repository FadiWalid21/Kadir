import { ChangeDetectionStrategy, Component, HostListener, input, signal } from '@angular/core';

@Component({
  selector: 'kadr-dropdown-menu',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'relative inline-flex',
  },
  template: `
    <button
      type="button"
      class="inline-flex items-center gap-2 rounded-md focus-visible:outline-none"
      [attr.aria-label]="ariaLabel()"
      [attr.aria-expanded]="open()"
      aria-haspopup="menu"
      (click)="toggle($event)"
    >
      <ng-content select="[dropdownTrigger]" />
    </button>

    @if (open()) {
      <div
        class="absolute end-0 top-full z-50 mt-1 min-w-44 rounded-md border border-line bg-surface p-1 shadow-md"
        role="menu"
        tabindex="-1"
        (keydown.escape)="close()"
      >
        <ng-content select="[dropdownContent]" />
      </div>
    }
  `,
})
export class DropdownMenuComponent {
  readonly ariaLabel = input.required<string>();
  protected readonly open = signal(false);

  toggle(event: Event): void {
    event.stopPropagation();
    this.open.update((v) => !v);
  }

  close(): void {
    this.open.set(false);
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.close();
  }
}
