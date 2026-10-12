import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  AttendanceApi,
  WorkLocation,
  WorkLocationAssignment,
  WorkLocationAssignmentInput,
} from '../../core/api/attendance.api';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { EmployeesApi } from '../../core/api/employees.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import {
  AssignmentTargetPickerComponent,
  AssignmentTargetSelection,
  AssignmentTargetType,
} from '../../shared/ui/assignment-target-picker/assignment-target-picker.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';

interface DraftRow {
  scope: AssignmentTargetType;
  targetId: string;
  effectiveFrom: string;
  effectiveTo: string;
  label: string;
}

@Component({
  selector: 'kadr-work-location-assign-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    AssignmentTargetPickerComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
  ],
  template: `
    <header class="mb-6 flex flex-col gap-2 text-start">
      <a routerLink="/attendance/locations" class="text-sm text-accent hover:underline">
        {{ 'attendance.locations.backToList' | transloco }}
      </a>
      <h1 class="text-2xl font-bold text-fg">{{ 'attendance.locations.assignTitle' | transloco }}</h1>
      @if (location(); as loc) {
        <p class="text-sm text-fg-2">{{ loc.nameAr }}</p>
      }
    </header>

    @if (state() === 'loading') {
      <kadr-skeleton height="8rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.locations.loadErrorTitle' | transloco"
        [description]="'attendance.locations.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else {
      <section class="mb-6 rounded-lg border border-line bg-surface p-4 text-start">
        <h2 class="mb-3 text-sm font-medium text-fg">{{ 'attendance.locations.addAssignment' | transloco }}</h2>
        <div class="flex flex-col gap-3">
          <kadr-assignment-target-picker
            [allowedTypes]="allowedTypes"
            [multiple]="false"
            [clearToken]="pickerClear()"
            (selectionChange)="onPickerChange($event)"
          />
          <div class="flex flex-wrap gap-2">
            <kadr-button
              variant="secondary"
              type="button"
              [disabled]="!canAdd()"
              (click)="addDraft()"
            >
              {{ 'attendance.locations.addRow' | transloco }}
            </kadr-button>
          </div>
        </div>
        <p class="mt-2 text-xs text-fg-muted">{{ 'attendance.locations.assignHint' | transloco }}</p>
      </section>

      @if (rows().length === 0) {
        <kadr-empty-state
          [title]="'attendance.locations.assignEmptyTitle' | transloco"
          [description]="'attendance.locations.assignEmptyBody' | transloco"
        />
      } @else {
        <ul class="mb-4 flex flex-col gap-2">
          @for (row of rows(); track $index) {
            <li
              class="flex flex-col gap-2 rounded-md border border-line bg-surface p-3 text-start sm:flex-row sm:items-center sm:justify-between"
            >
              <div class="min-w-0 text-sm">
                <p class="font-medium text-fg">
                  <span class="text-fg-2">{{ scopeLabel(row.scope) }} · </span>
                  <span class="truncate" [attr.title]="row.label">{{ row.label }}</span>
                </p>
              </div>
              <kadr-button variant="ghost" type="button" (click)="removeRow($index)">
                {{ 'ui.delete' | transloco }}
              </kadr-button>
            </li>
          }
        </ul>
      }

      <div class="flex justify-end">
        <kadr-button
          variant="primary"
          type="button"
          [loading]="saving()"
          [disabled]="saving()"
          (click)="save()"
        >
          {{ 'ui.save' | transloco }}
        </kadr-button>
      </div>
    }
  `,
})
export class WorkLocationAssignPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly employeesApi = inject(EmployeesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly allowedTypes: AssignmentTargetType[] = ['Employee', 'Department', 'Branch'];

  protected readonly state = signal<PageState>('loading');
  protected readonly location = signal<WorkLocation | null>(null);
  protected readonly rows = signal<DraftRow[]>([]);
  protected readonly saving = signal(false);
  protected readonly draft = signal<AssignmentTargetSelection | null>(null);
  protected readonly pickerClear = signal(0);

  private locationId = '';

  ngOnInit(): void {
    this.locationId = this.route.snapshot.paramMap.get('id') ?? '';
    this.title.setTitle(this.transloco.translate('attendance.locations.assignTitle'));
    void this.reload();
  }

  protected canAdd(): boolean {
    const d = this.draft();
    return !!d && d.targets.length === 1;
  }

  protected onPickerChange(sel: AssignmentTargetSelection): void {
    this.draft.set(sel);
  }

  protected scopeLabel(scope: string): string {
    switch (scope) {
      case 'Department':
        return this.transloco.translate('attendance.locations.scopeDepartment');
      case 'Branch':
        return this.transloco.translate('attendance.locations.scopeBranch');
      default:
        return this.transloco.translate('attendance.locations.scopeEmployee');
    }
  }

  protected addDraft(): void {
    const d = this.draft();
    const target = d?.targets[0];
    if (!d || !target) {
      return;
    }
    this.rows.update((list) => [
      ...list,
      {
        scope: d.type,
        targetId: target.id,
        effectiveFrom: '',
        effectiveTo: '',
        label: target.name,
      },
    ]);
    this.draft.set({ type: d.type, targets: [] });
    this.pickerClear.update((n) => n + 1);
  }

  protected removeRow(index: number): void {
    this.rows.update((list) => list.filter((_, i) => i !== index));
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const [loc, assignments] = await Promise.all([
        this.api.getWorkLocation(this.locationId),
        this.api.getWorkLocationAssignments(this.locationId),
      ]);
      this.location.set(loc);
      const rows = await Promise.all(assignments.map((a) => this.fromDto(a)));
      this.rows.set(rows);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected async save(): Promise<void> {
    this.saving.set(true);
    try {
      const body: WorkLocationAssignmentInput[] = this.rows().map((r) => ({
        scope: r.scope,
        employeeId: r.scope === 'Employee' ? r.targetId : null,
        departmentId: r.scope === 'Department' ? r.targetId : null,
        branchId: r.scope === 'Branch' ? r.targetId : null,
        effectiveFrom: r.effectiveFrom || null,
        effectiveTo: r.effectiveTo || null,
      }));
      const saved = await this.api.replaceWorkLocationAssignments(this.locationId, body);
      const rows = await Promise.all(saved.map((a) => this.fromDto(a)));
      this.rows.set(rows);
      this.toast.show(this.transloco.translate('attendance.settings.saved'));
    } catch (err) {
      const pd = problemDetailsFromError(err);
      this.toast.show(
        pd?.detail ?? this.transloco.translate('attendance.settings.saveFailed'),
        'danger',
      );
    } finally {
      this.saving.set(false);
    }
  }

  private async fromDto(a: WorkLocationAssignment): Promise<DraftRow> {
    const targetId = a.employeeId ?? a.departmentId ?? a.branchId ?? '';
    let label = targetId;
    const scope = (a.scope as AssignmentTargetType) || 'Employee';
    try {
      if (a.scope === 'Employee' && a.employeeId) {
        const emp = await this.employeesApi.get(a.employeeId);
        label = emp.fullNameAr || emp.fullNameEn || targetId;
      } else if (a.scope === 'Department' && a.departmentId) {
        const org = await this.employeesApi.orgLookups();
        const lang = this.transloco.getActiveLang();
        const d = org.departments.find((x) => x.id === a.departmentId);
        label = (lang === 'ar' ? d?.nameAr : d?.nameEn) || d?.nameAr || d?.nameEn || targetId;
      } else if (a.scope === 'Branch' && a.branchId) {
        const org = await this.employeesApi.orgLookups();
        const b = org.branches.find((x) => x.id === a.branchId);
        label = b?.nameAr || b?.nameEn || targetId;
      }
    } catch {
      label = targetId;
    }
    if (/^[0-9a-f-]{36}$/i.test(label)) {
      label = this.scopeLabel(a.scope);
    }
    return {
      scope,
      targetId,
      effectiveFrom: a.effectiveFrom ?? '',
      effectiveTo: a.effectiveTo ?? '',
      label,
    };
  }
}
