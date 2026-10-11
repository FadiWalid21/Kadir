import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { EmployeesApi, OrgDepartmentNode } from '../../core/api/employees.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { SelectComponent, SelectOption } from '../../shared/ui/select/select.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error' | 'empty' | 'locked';

interface FlatDept {
  node: OrgDepartmentNode;
  depth: number;
}

@Component({
  selector: 'kadr-org-tree-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    SkeletonComponent,
    EmptyStateComponent,
    SelectComponent,
  ],
  template: `
    <div class="flex flex-col gap-5">
      <header class="text-start">
        <h1 class="text-2xl font-bold text-fg">{{ 'employees.orgTitle' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'employees.orgSubtitle' | transloco }}</p>
      </header>

      @if (state() === 'locked') {
        <kadr-empty-state
          [title]="'employees.featureLockedTitle' | transloco"
          [description]="'employees.featureLockedBody' | transloco"
        />
      } @else if (state() === 'loading') {
        <kadr-skeleton height="10rem" />
      } @else if (state() === 'error') {
        <kadr-empty-state
          [title]="'employees.loadErrorTitle' | transloco"
          [description]="'employees.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">{{
            'home.retry' | transloco
          }}</kadr-button>
        </kadr-empty-state>
      } @else if (state() === 'empty') {
        <kadr-empty-state
          [title]="'employees.orgEmptyTitle' | transloco"
          [description]="'employees.orgEmptyBody' | transloco"
        >
          <a routerLink="/settings/templates" class="contents">
            <kadr-button variant="primary" type="button">{{
              'employees.orgGoTemplates' | transloco
            }}</kadr-button>
          </a>
        </kadr-empty-state>
      } @else {
        <ul class="flex flex-col gap-2">
          @for (row of rows(); track row.node.id) {
            <li
              class="rounded-lg border border-line bg-surface p-3"
              [style.margin-inline-start.rem]="row.depth * 1.25"
            >
              <div class="flex flex-wrap items-start justify-between gap-2">
                <div class="min-w-0 text-start">
                  <p class="font-medium text-fg">{{ deptName(row.node) }}</p>
                  <p class="text-xs text-fg-2">
                    {{ 'employees.orgHeadcount' | transloco: { n: row.node.employeeCount } }}
                    @if (row.node.managerNameAr) {
                      · {{ 'employees.manager' | transloco }}: {{ row.node.managerNameAr }}
                    }
                  </p>
                </div>
                <div class="flex flex-wrap gap-2">
                  <a
                    [routerLink]="['/employees']"
                    [queryParams]="{ departmentId: row.node.id }"
                    class="contents"
                  >
                    <kadr-button variant="ghost" size="sm" type="button">{{
                      'employees.orgViewEmployees' | transloco
                    }}</kadr-button>
                  </a>
                  @if (canManage()) {
                    <kadr-button
                      variant="secondary"
                      size="sm"
                      type="button"
                      (click)="edit(row.node)"
                    >
                      {{ 'employees.orgEdit' | transloco }}
                    </kadr-button>
                  }
                </div>
              </div>
              @if (editingId() === row.node.id) {
                <div class="mt-3 grid gap-3 border-t border-line pt-3 sm:grid-cols-2">
                  <kadr-select
                    [label]="'employees.orgParent' | transloco"
                    [options]="parentOptions(row.node.id)"
                    [value]="editParentId()"
                    (valueChange)="editParentId.set($event)"
                  />
                  <kadr-select
                    [label]="'employees.manager' | transloco"
                    [options]="managerOptions()"
                    [value]="editManagerId()"
                    (valueChange)="editManagerId.set($event)"
                  />
                  <div class="flex flex-wrap gap-2 sm:col-span-2">
                    <kadr-button
                      variant="primary"
                      size="sm"
                      type="button"
                      [loading]="saving()"
                      (click)="saveEdit(row.node.id)"
                    >
                      {{ 'ui.save' | transloco }}
                    </kadr-button>
                    <kadr-button
                      variant="secondary"
                      size="sm"
                      type="button"
                      (click)="editingId.set(null)"
                    >
                      {{ 'ui.cancel' | transloco }}
                    </kadr-button>
                  </div>
                </div>
              }
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class OrgTreePage implements OnInit {
  private readonly api = inject(EmployeesApi);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly rows = signal<FlatDept[]>([]);
  protected readonly flat = signal<OrgDepartmentNode[]>([]);
  protected readonly managerOptions = signal<SelectOption[]>([{ value: '', label: '—' }]);
  protected readonly editingId = signal<string | null>(null);
  protected readonly editParentId = signal('');
  protected readonly editManagerId = signal('');
  protected readonly saving = signal(false);

  ngOnInit(): void {
    void this.reload();
  }

  canManage(): boolean {
    return this.auth.hasPermission(PermissionCodes.OrgManage);
  }

  reload(): void {
    void this.load();
  }

  deptName(node: OrgDepartmentNode): string {
    return this.i18n.getActiveLang() === 'en' ? node.nameEn || node.nameAr : node.nameAr;
  }

  parentOptions(excludeId: string): SelectOption[] {
    return [
      { value: '', label: this.i18n.translate('employees.orgNoParent') },
      ...this.flat()
        .filter((d) => d.id !== excludeId)
        .map((d) => ({
          value: d.id,
          label: this.deptName(d),
        })),
    ];
  }

  edit(node: OrgDepartmentNode): void {
    this.editingId.set(node.id);
    this.editParentId.set(node.parentId ?? '');
    this.editManagerId.set(node.managerEmployeeId ?? '');
  }

  async saveEdit(id: string): Promise<void> {
    this.saving.set(true);
    try {
      await this.api.updateDepartment(id, {
        parentId: this.editParentId() || null,
        managerEmployeeId: this.editManagerId() || null,
      });
      this.editingId.set(null);
      this.toast.show(this.i18n.translate('employees.saved'), 'success');
      await this.load();
    } catch {
      this.toast.show(this.i18n.translate('employees.saveError'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }

  private flatten(nodes: OrgDepartmentNode[], depth = 0): FlatDept[] {
    const out: FlatDept[] = [];
    for (const n of nodes) {
      out.push({ node: n, depth });
      out.push(...this.flatten(n.children, depth + 1));
    }
    return out;
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      const [tree, managers] = await Promise.all([
        this.api.orgTree(),
        this.api.list({ page: 1, pageSize: 100 }),
      ]);
      const flatRows = this.flatten(tree);
      this.rows.set(flatRows);
      this.flat.set(flatRows.map((r) => r.node));
      this.managerOptions.set([
        { value: '', label: '—' },
        ...managers.items.map((m) => ({
          value: m.id,
          label:
            this.i18n.getActiveLang() === 'en' && m.fullNameEn ? m.fullNameEn : m.fullNameAr,
        })),
      ]);
      this.state.set(tree.length === 0 ? 'empty' : 'ready');
    } catch (err: unknown) {
      const code = (err as { error?: { code?: string } })?.error?.code;
      this.state.set(code === 'plan.feature_not_available' ? 'locked' : 'error');
    }
  }
}
