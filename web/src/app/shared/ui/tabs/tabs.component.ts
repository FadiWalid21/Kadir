import { ChangeDetectionStrategy, Component, input, model, output } from '@angular/core';

export interface TabItem {
  id: string;
  label: string;
}

@Component({
  selector: 'kadr-tabs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="flex flex-wrap gap-1 border-b border-line"
      role="tablist"
      [attr.aria-label]="ariaLabel()"
    >
      @for (tab of tabs(); track tab.id) {
        <button
          type="button"
          role="tab"
          class="relative -mb-px border-b-2 px-3 py-2 text-sm font-medium transition-colors focus-visible:outline-none"
          [class.border-accent]="activeId() === tab.id"
          [class.text-fg]="activeId() === tab.id"
          [class.border-transparent]="activeId() !== tab.id"
          [class.text-fg-2]="activeId() !== tab.id"
          [class.hover:text-fg]="activeId() !== tab.id"
          [attr.aria-selected]="activeId() === tab.id"
          [attr.id]="'tab-' + tab.id"
          (click)="select(tab.id)"
        >
          {{ tab.label }}
        </button>
      }
    </div>
  `,
})
export class TabsComponent {
  readonly tabs = input.required<TabItem[]>();
  readonly ariaLabel = input.required<string>();
  readonly activeId = model.required<string>();
  readonly activeChange = output<string>();

  select(id: string): void {
    this.activeId.set(id);
    this.activeChange.emit(id);
  }
}
