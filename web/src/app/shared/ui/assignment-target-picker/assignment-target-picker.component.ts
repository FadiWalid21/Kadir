import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LookupsApi } from '../../../core/api/lookups.api';
import { ButtonComponent } from '../button/button.component';
import {
  ComboboxComponent,
  ComboboxOption,
  ComboboxSearchFn,
} from '../combobox/combobox.component';
import { IconComponent } from '../icon/icon.component';
import { SelectComponent, SelectOption } from '../select/select.component';

export type AssignmentTargetType = 'Employee' | 'Department' | 'Branch' | 'JobTitle';

export interface AssignmentTargetSelection {
  type: AssignmentTargetType;
  targets: ComboboxOption[];
}

@Component({
  selector: 'kadr-assignment-target-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    SelectComponent,
    ComboboxComponent,
    ButtonComponent,
    IconComponent,
  ],
  template: `
    <div class="grid gap-3" [attr.aria-label]="'ui.assignmentTarget.aria' | transloco">
      <kadr-select
        [label]="'ui.assignmentTarget.assignTo' | transloco"
        [options]="typeOptions()"
        [value]="type()"
        [placeholder]="'ui.assignmentTarget.chooseType' | transloco"
        [disabled]="disabled()"
        (valueChange)="onTypeChange($event)"
      />

      <div class="grid gap-2">
        <kadr-combobox
          [label]="targetLabel()"
          [placeholder]="targetPlaceholder()"
          [searchFn]="searchFn"
          [value]="draftId()"
          [displayName]="draftName()"
          [disabled]="disabled() || !type()"
          [hint]="!type() ? ('ui.assignmentTarget.pickTypeFirst' | transloco) : undefined"
          (selectionChange)="onTargetPicked($event)"
        />

        @if (multiple() && selected().length > 0) {
          <ul class="flex flex-wrap gap-2" role="list">
            @for (t of selected(); track t.id) {
              <li
                class="inline-flex max-w-full items-center gap-1 rounded-md bg-accent-soft px-2 py-1 text-sm text-accent-soft-fg"
              >
                <span class="truncate" [attr.title]="t.name">{{ t.name }}</span>
                <button
                  type="button"
                  class="inline-flex size-8 shrink-0 items-center justify-center rounded-md hover:bg-hover focus-visible:outline-none"
                  [attr.aria-label]="'ui.assignmentTarget.remove' | transloco: { name: t.name }"
                  [disabled]="disabled()"
                  (click)="removeTarget(t.id)"
                >
                  <kadr-icon name="x" size="sm" />
                </button>
              </li>
            }
          </ul>
        }
      </div>
    </div>
  `,
})
export class AssignmentTargetPickerComponent {
  private readonly lookups = inject(LookupsApi);
  private readonly i18n = inject(TranslocoService);
  private readonly combobox = viewChild(ComboboxComponent);

  readonly allowedTypes = input.required<AssignmentTargetType[]>();
  readonly multiple = input(false);
  readonly disabled = input(false);
  /** Controlled type (optional); empty until chosen. */
  readonly typeValue = input<AssignmentTargetType | ''>('');
  readonly selectedValue = input<ComboboxOption[]>([]);
  /** Bump to clear selection while keeping the current type. */
  readonly clearToken = input(0);

  readonly selectionChange = output<AssignmentTargetSelection>();

  protected readonly type = signal<AssignmentTargetType | ''>('');
  protected readonly selected = signal<ComboboxOption[]>([]);
  protected readonly draftId = signal('');
  protected readonly draftName = signal('');

  protected readonly typeOptions = computed<SelectOption[]>(() =>
    this.allowedTypes().map((t) => ({
      value: t,
      label: this.i18n.translate(`ui.assignmentTarget.types.${t}`),
    })),
  );

  protected readonly targetLabel = computed(() =>
    this.i18n.translate('ui.assignmentTarget.target'),
  );

  protected readonly targetPlaceholder = computed(() =>
    this.i18n.translate('ui.assignmentTarget.search'),
  );

  /** Stable searchFn; reads current type from signal at call time. */
  protected readonly searchFn: ComboboxSearchFn = (q) => this.search(q);

  constructor() {
    effect(() => {
      const external = this.typeValue();
      if (external !== this.type()) {
        this.type.set(external);
      }
    });
    effect(() => {
      this.selected.set([...this.selectedValue()]);
    });
    effect(() => {
      const token = this.clearToken();
      if (token > 0) {
        this.selected.set([]);
        this.draftId.set('');
        this.draftName.set('');
        this.combobox()?.reset();
      }
    });
  }

  protected onTypeChange(value: string): void {
    const next = (value || '') as AssignmentTargetType | '';
    this.type.set(next);
    this.selected.set([]);
    this.draftId.set('');
    this.draftName.set('');
    this.combobox()?.reset();
    this.emit();
  }

  protected onTargetPicked(opt: ComboboxOption | null): void {
    if (!opt || !this.type()) {
      this.draftId.set('');
      this.draftName.set('');
      if (!this.multiple()) {
        this.selected.set([]);
        this.emit();
      }
      return;
    }

    if (this.multiple()) {
      if (!this.selected().some((x) => x.id === opt.id)) {
        this.selected.update((list) => [...list, opt]);
      }
      this.draftId.set('');
      this.draftName.set('');
      this.combobox()?.reset();
    } else {
      this.selected.set([opt]);
      this.draftId.set(opt.id);
      this.draftName.set(opt.name);
    }
    this.emit();
  }

  protected removeTarget(id: string): void {
    this.selected.update((list) => list.filter((x) => x.id !== id));
    this.emit();
  }

  private emit(): void {
    const t = this.type();
    if (!t) {
      return;
    }
    this.selectionChange.emit({ type: t, targets: [...this.selected()] });
  }

  private async search(q: string): Promise<ComboboxOption[]> {
    const t = this.type();
    if (!t) {
      return [];
    }
    const page =
      t === 'Department'
        ? await this.lookups.searchDepartments(q)
        : t === 'Branch'
          ? await this.lookups.searchBranches(q)
          : t === 'JobTitle'
            ? await this.lookups.searchJobTitles(q)
            : await this.lookups.searchEmployees(q);
    const selectedIds = new Set(this.selected().map((x) => x.id));
    return page.items
      .filter((i) => !selectedIds.has(i.id))
      .map((i) => ({ id: i.id, name: i.name }));
  }
}
