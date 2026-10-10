import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface TableColumn {
  id: string;
  label: string;
  className?: string;
}

@Component({
  selector: 'kadr-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="overflow-x-auto rounded-md border border-line">
      <table class="w-full min-w-full border-collapse text-start text-sm">
        <thead class="sticky top-0 z-10 bg-subtle">
          <tr class="border-b border-line">
            @for (col of columns(); track col.id) {
              <th
                scope="col"
                class="px-3 py-2.5 text-start text-xs font-medium text-fg-2"
                [class]="col.className ?? ''"
              >
                {{ col.label }}
              </th>
            }
          </tr>
        </thead>
        <tbody>
          <ng-content />
        </tbody>
      </table>
    </div>
  `,
})
export class TableComponent {
  readonly columns = input.required<TableColumn[]>();
}
