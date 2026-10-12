import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  AttendanceApi,
  PreviewShiftAssignmentRules,
  ShiftAssignmentRule,
} from '../../core/api/attendance.api';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { TenantTemplatesApi, TemplateItem } from '../../core/api/tenant-templates.api';
import {
  AssignmentTargetPickerComponent,
  AssignmentTargetSelection,
  AssignmentTargetType,
} from '../../shared/ui/assignment-target-picker/assignment-target-picker.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-shift-rules-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    DrawerComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
    InputComponent,
    AssignmentTargetPickerComponent,
  ],
  template: `
    <header class="mb-4 flex flex-wrap items-start justify-between gap-3 text-start">
      <div>
        <a routerLink="/attendance/schedules" class="text-sm text-accent hover:underline">
          {{ 'attendance.shiftRules.backToWeek' | transloco }}
        </a>
        <h1 class="mt-1 text-2xl font-bold text-fg">{{ 'attendance.shiftRules.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.shiftRules.subtitle' | transloco }}</p>
      </div>
      <kadr-button variant="primary" type="button" (click)="openCreate()">
        {{ 'attendance.shiftRules.add' | transloco }}
      </kadr-button>
    </header>

    <div class="mb-4 flex flex-wrap gap-2">
      <label class="flex flex-col gap-1 text-sm text-fg">
        <span class="font-medium">{{ 'attendance.shiftRules.filterType' | transloco }}</span>
        <select
          class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
          [ngModel]="typeFilter()"
          (ngModelChange)="onTypeFilter($event)"
        >
          <option value="">{{ 'attendance.shiftRules.filterAll' | transloco }}</option>
          @for (t of allowedTypes; track t) {
            <option [value]="t">{{ 'ui.assignmentTarget.types.' + t | transloco }}</option>
          }
        </select>
      </label>
    </div>

    @if (state() === 'loading') {
      <kadr-skeleton height="12rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.shiftRules.loadErrorTitle' | transloco"
        [description]="'attendance.shiftRules.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (items().length === 0) {
      <kadr-empty-state
        [title]="'attendance.shiftRules.emptyTitle' | transloco"
        [description]="'attendance.shiftRules.emptyBody' | transloco"
      >
        <kadr-button variant="primary" type="button" (click)="openCreate()">
          {{ 'attendance.shiftRules.add' | transloco }}
        </kadr-button>
      </kadr-empty-state>
    } @else {
      <ul class="flex flex-col gap-2">
        @for (rule of items(); track rule.id) {
          <li
            class="flex flex-col gap-2 rounded-md border border-line bg-surface p-3 text-start sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="min-w-0 text-sm">
              <p class="font-medium text-fg">
                <span class="text-fg-2">{{ typeLabel(rule.targetType) }} · </span>
                <span class="truncate" [attr.title]="rule.targetName ?? ''">{{
                  rule.targetName || '—'
                }}</span>
              </p>
              <p class="mt-1 text-fg-2">
                @if (rule.isDayOff) {
                  {{ 'attendance.shiftRules.dayOff' | transloco }}
                } @else {
                  {{ shiftLabel(rule) }}
                }
                · {{ rule.effectiveFrom }}
                @if (rule.effectiveTo) {
                  – {{ rule.effectiveTo }}
                } @else {
                  – {{ 'attendance.shiftRules.openEnded' | transloco }}
                }
              </p>
            </div>
            <kadr-button
              variant="ghost"
              type="button"
              [disabled]="endingId() === rule.id"
              (click)="endRule(rule)"
            >
              {{ 'attendance.shiftRules.end' | transloco }}
            </kadr-button>
          </li>
        }
      </ul>
      @if (totalCount() > items().length) {
        <p class="mt-3 text-xs text-fg-muted">
          {{
            'attendance.shiftRules.showing'
              | transloco: { shown: items().length, total: totalCount() }
          }}
        </p>
      }
    }

    <kadr-drawer
      [open]="drawerOpen()"
      [title]="'attendance.shiftRules.addTitle' | transloco"
      [closeLabel]="'ui.cancel' | transloco"
      [hasFooter]="false"
      (closed)="closeDrawer()"
    >
      <form class="flex flex-col gap-4" (ngSubmit)="onSave()">
        <kadr-assignment-target-picker
          [allowedTypes]="allowedTypes"
          [multiple]="true"
          [clearToken]="pickerClear()"
          (selectionChange)="onPickerChange($event)"
        />

        <label class="flex flex-col gap-1 text-sm text-fg">
          <span class="font-medium">{{ 'attendance.shiftRules.shift' | transloco }}</span>
          <select
            class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
            [ngModel]="shiftId()"
            (ngModelChange)="shiftId.set($event)"
            name="shiftId"
            [disabled]="isDayOff()"
          >
            <option value="">{{ 'attendance.schedules.pickShift' | transloco }}</option>
            @for (s of shifts(); track s.id) {
              <option [value]="s.id">{{ templateName(s) }}</option>
            }
          </select>
        </label>

        <label class="inline-flex items-center gap-2 text-sm text-fg">
          <input
            type="checkbox"
            class="size-4 rounded border-line"
            [checked]="isDayOff()"
            (change)="onDayOffToggle($event)"
          />
          {{ 'attendance.shiftRules.dayOff' | transloco }}
        </label>

        <kadr-input
          type="date"
          [label]="'attendance.shiftRules.effectiveFrom' | transloco"
          [value]="effectiveFrom()"
          (valueChange)="effectiveFrom.set($event)"
        />
        <kadr-input
          type="date"
          [label]="'attendance.shiftRules.effectiveTo' | transloco"
          [value]="effectiveTo()"
          (valueChange)="effectiveTo.set($event)"
        />

        <label class="inline-flex items-start gap-2 text-sm text-fg">
          <input
            type="checkbox"
            class="mt-0.5 size-4 rounded border-line"
            [checked]="confirmRecalc()"
            (change)="confirmRecalc.set($any($event.target).checked)"
          />
          <span>{{ 'attendance.shiftRules.confirmRecalc' | transloco }}</span>
        </label>
        <p class="text-xs text-fg-muted">{{ 'attendance.shiftRules.backdateNote' | transloco }}</p>

        <div class="rounded-md border border-line bg-subtle p-3 text-sm text-fg-2">
          @if (previewBusy()) {
            <p>{{ 'attendance.shiftRules.previewLoading' | transloco }}</p>
          } @else if (preview(); as p) {
            <p>
              {{
                'attendance.shiftRules.previewSummary'
                  | transloco: { affected: p.affectedEmployeeCount, overrides: p.overrideCount }
              }}
            </p>
          } @else {
            <p>{{ 'attendance.shiftRules.previewHint' | transloco }}</p>
          }
          <kadr-button
            class="mt-2"
            variant="secondary"
            type="button"
            [disabled]="!canPreview()"
            [loading]="previewBusy()"
            (click)="runPreview()"
          >
            {{ 'attendance.shiftRules.preview' | transloco }}
          </kadr-button>
        </div>

        <div class="mt-2 flex flex-wrap justify-end gap-2">
          <kadr-button variant="secondary" type="button" (click)="closeDrawer()">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
          <kadr-button variant="primary" type="submit" [disabled]="!canSave()" [loading]="busy()">
            {{ 'ui.save' | transloco }}
          </kadr-button>
        </div>
      </form>
    </kadr-drawer>
  `,
})
export class ShiftRulesPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly templates = inject(TenantTemplatesApi);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly title = inject(Title);

  readonly allowedTypes: AssignmentTargetType[] = [
    'Employee',
    'JobTitle',
    'Department',
    'Branch',
  ];

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<ShiftAssignmentRule[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly typeFilter = signal('');
  protected readonly shifts = signal<TemplateItem[]>([]);
  protected readonly drawerOpen = signal(false);
  protected readonly busy = signal(false);
  protected readonly endingId = signal<string | null>(null);
  protected readonly pickerClear = signal(0);
  protected readonly selection = signal<AssignmentTargetSelection | null>(null);
  protected readonly shiftId = signal('');
  protected readonly isDayOff = signal(false);
  protected readonly effectiveFrom = signal(todayIso());
  protected readonly effectiveTo = signal('');
  protected readonly confirmRecalc = signal(false);
  protected readonly preview = signal<PreviewShiftAssignmentRules | null>(null);
  protected readonly previewBusy = signal(false);

  protected readonly canPreview = computed(() => {
    const sel = this.selection();
    return !!sel?.type && sel.targets.length > 0 && !!this.effectiveFrom();
  });

  protected readonly canSave = computed(() => {
    const sel = this.selection();
    if (!sel?.type || sel.targets.length === 0 || !this.effectiveFrom()) return false;
    if (!this.isDayOff() && !this.shiftId()) return false;
    return true;
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.shiftRules.title'));
    void this.templates
      .listSection('shifts', { pageSize: 50, active: true })
      .then((p) => this.shifts.set(p.items))
      .catch(() => undefined);
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const page = await this.api.listShiftRules({
        page: 1,
        pageSize: 50,
        targetType: this.typeFilter() || undefined,
      });
      this.items.set(page.items);
      this.totalCount.set(page.totalCount);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected onTypeFilter(value: string): void {
    this.typeFilter.set(value);
    void this.reload();
  }

  protected openCreate(): void {
    this.selection.set(null);
    this.shiftId.set('');
    this.isDayOff.set(false);
    this.effectiveFrom.set(todayIso());
    this.effectiveTo.set('');
    this.confirmRecalc.set(false);
    this.preview.set(null);
    this.pickerClear.update((n) => n + 1);
    this.drawerOpen.set(true);
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  protected onPickerChange(sel: AssignmentTargetSelection): void {
    this.selection.set(sel);
    this.preview.set(null);
  }

  protected onDayOffToggle(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.isDayOff.set(checked);
    if (checked) this.shiftId.set('');
  }

  protected async runPreview(): Promise<void> {
    const sel = this.selection();
    if (!sel?.type || sel.targets.length === 0) return;
    this.previewBusy.set(true);
    try {
      const result = await this.api.previewShiftRules({
        targetType: sel.type,
        targetIds: sel.targets.map((t) => t.id),
        effectiveFrom: this.effectiveFrom(),
        effectiveTo: this.effectiveTo() || null,
      });
      this.preview.set(result);
    } catch {
      this.toast.show(this.transloco.translate('attendance.shiftRules.previewFailed'), 'danger');
    } finally {
      this.previewBusy.set(false);
    }
  }

  protected async onSave(): Promise<void> {
    const sel = this.selection();
    if (!sel?.type || !this.canSave()) return;
    this.busy.set(true);
    try {
      await this.api.createShiftRules({
        targetType: sel.type,
        targetIds: sel.targets.map((t) => t.id),
        shiftId: this.isDayOff() ? null : this.shiftId() || null,
        isDayOff: this.isDayOff(),
        effectiveFrom: this.effectiveFrom(),
        effectiveTo: this.effectiveTo() || null,
        confirmRecalc: this.confirmRecalc(),
      });
      this.toast.show(this.transloco.translate('attendance.shiftRules.saved'), 'success');
      this.closeDrawer();
      await this.reload();
    } catch (err) {
      const pd = problemDetailsFromError(err);
      const code = pd?.code ?? '';
      if (code === 'attendance.shift_rule_overlap') {
        this.toast.show(this.transloco.translate('attendance.shiftRules.overlap'), 'danger');
      } else if (code === 'attendance.shift_rule_backdate_requires_confirm') {
        this.toast.show(this.transloco.translate('attendance.shiftRules.needConfirm'), 'danger');
      } else {
        this.toast.show(this.transloco.translate('attendance.shiftRules.saveFailed'), 'danger');
      }
    } finally {
      this.busy.set(false);
    }
  }

  protected async endRule(rule: ShiftAssignmentRule): Promise<void> {
    this.endingId.set(rule.id);
    try {
      await this.api.endShiftRule(rule.id, todayIso());
      this.toast.show(this.transloco.translate('attendance.shiftRules.ended'), 'success');
      await this.reload();
    } catch {
      this.toast.show(this.transloco.translate('attendance.shiftRules.endFailed'), 'danger');
    } finally {
      this.endingId.set(null);
    }
  }

  protected typeLabel(type: string): string {
    return this.transloco.translate(`ui.assignmentTarget.types.${type}`);
  }

  protected shiftLabel(rule: ShiftAssignmentRule): string {
    const lang = this.transloco.getActiveLang();
    if (lang === 'en') return rule.shiftNameEn || rule.shiftNameAr || '—';
    return rule.shiftNameAr || rule.shiftNameEn || '—';
  }

  protected templateName(item: TemplateItem): string {
    const lang = this.transloco.getActiveLang();
    return lang === 'en' ? item.nameEn || item.nameAr : item.nameAr || item.nameEn;
  }
}

function todayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
