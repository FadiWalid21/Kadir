import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  EmployeesApi,
  EmployeeListItem,
  EmployeeSummary,
  OrgLookups,
} from '../../core/api/employees.api';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { AuthService } from '../../core/auth/auth.service';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { SelectComponent, SelectOption } from '../../shared/ui/select/select.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';

type PageState = 'loading' | 'ready' | 'error' | 'empty' | 'locked';

@Component({
  selector: 'kadr-employees-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
    AvatarComponent,
    SelectComponent,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <header class="flex flex-wrap items-start justify-between gap-3">
        <div class="text-start">
          <div class="flex flex-wrap items-center gap-2">
            <h1 class="text-2xl font-bold text-fg">{{ 'employees.title' | transloco }}</h1>
            @if (summary(); as s) {
              <span class="rounded-full bg-subtle px-2 py-0.5 text-xs tabular-nums text-fg-2">{{
                s.statusCounts.all
              }}</span>
            }
          </div>
          @if (summary(); as s) {
            <p class="mt-1 text-sm text-fg-2">
              {{
                'employees.capacityLine'
                  | transloco
                    : {
                        active: s.activeForCapacity,
                        cap: s.employeeCap ?? '—',
                      }
              }}
            </p>
            @if (s.employeeCap) {
              <div class="mt-2 h-2 max-w-xs overflow-hidden rounded-full bg-subtle">
                <div
                  class="h-full rounded-full bg-accent"
                  [style.width.%]="capacityPct()"
                ></div>
              </div>
            }
          }
        </div>
        <div class="flex flex-wrap gap-2">
          @if (canImport()) {
            <a routerLink="/employees/import" class="contents">
              <kadr-button variant="secondary" type="button">{{
                'employees.import' | transloco
              }}</kadr-button>
            </a>
          }
          @if (canImportSalaries()) {
            <a routerLink="/employees/salaries/import" class="contents">
              <kadr-button variant="secondary" type="button">{{
                'employees.salary.import' | transloco
              }}</kadr-button>
            </a>
          }
          @if (canCreate()) {
            <a routerLink="/employees/new" class="contents">
              <kadr-button variant="primary" type="button">{{
                'employees.add' | transloco
              }}</kadr-button>
            </a>
          }
        </div>
      </header>

      @if (atCap()) {
        <div
          class="flex flex-wrap items-center justify-between gap-2 rounded-md bg-warning-soft px-3 py-3 text-sm text-warning"
        >
          <p>{{ 'employees.capReached' | transloco: { cap: summary()?.employeeCap } }}</p>
          <a routerLink="/settings/subscription" class="font-medium underline">{{
            'employees.viewPlans' | transloco
          }}</a>
        </div>
      }

      @if (state() === 'locked') {
        <kadr-empty-state
          [title]="'employees.featureLockedTitle' | transloco"
          [description]="'employees.featureLockedBody' | transloco"
        >
          <a routerLink="/settings/subscription" class="contents">
            <kadr-button variant="primary" type="button">{{
              'employees.viewPlans' | transloco
            }}</kadr-button>
          </a>
        </kadr-empty-state>
      } @else if (state() === 'loading') {
        <div class="flex flex-col gap-2">
          @for (_ of [1, 2, 3, 4, 5]; track $index) {
            <kadr-skeleton height="3.5rem" />
          }
        </div>
      } @else if (state() === 'error') {
        <kadr-empty-state
          [title]="'employees.loadErrorTitle' | transloco"
          [description]="'employees.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">{{
            'home.retry' | transloco
          }}</kadr-button>
        </kadr-empty-state>
      } @else {
        <div class="flex flex-wrap gap-1" role="tablist">
          @for (tab of statusTabs; track tab.value) {
            <button
              type="button"
              role="tab"
              class="h-9 rounded-full px-3 text-xs focus-visible:outline-none"
              [class.bg-accent]="status() === tab.value"
              [class.text-on-accent]="status() === tab.value"
              [class.bg-subtle]="status() !== tab.value"
              [class.text-fg-2]="status() !== tab.value"
              (click)="setStatus(tab.value)"
            >
              {{ tab.labelKey | transloco }}
              @if (summary(); as s) {
                <span class="ms-1 tabular-nums">({{ statusCount(s, tab.value) }})</span>
              }
            </button>
          }
        </div>

        <div class="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4">
          <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <label class="grid min-w-0 gap-1 text-xs text-fg-2">
              <span>{{ 'employees.search' | transloco }}</span>
              <input
                class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm"
                [ngModel]="q()"
                (ngModelChange)="q.set($event)"
                (keyup.enter)="reload()"
              />
            </label>
            <kadr-select
              [label]="'employees.filterDepartment' | transloco"
              [options]="departmentOptions()"
              [value]="departmentId()"
              (valueChange)="departmentId.set($event)"
            />
            <kadr-select
              [label]="'employees.filterBranch' | transloco"
              [options]="branchOptions()"
              [value]="branchId()"
              (valueChange)="branchId.set($event)"
            />
            <kadr-select
              [label]="'employees.filterJob' | transloco"
              [options]="jobOptions()"
              [value]="jobTitleId()"
              (valueChange)="jobTitleId.set($event)"
            />
          </div>
          <div class="flex flex-wrap gap-2">
            <kadr-button variant="primary" type="button" (click)="reload()">{{
              'employees.apply' | transloco
            }}</kadr-button>
            <kadr-button variant="secondary" type="button" (click)="clearFilters()">{{
              'employees.clearFilters' | transloco
            }}</kadr-button>
            <a routerLink="/documents/expiring" class="contents">
              <kadr-button variant="ghost" type="button">{{
                'employees.expiringLink' | transloco
              }}</kadr-button>
            </a>
          </div>
        </div>

        @if (state() === 'empty') {
          <kadr-empty-state
            [title]="'employees.emptyTitle' | transloco"
            [description]="'employees.emptyBody' | transloco"
          >
            @if (canCreate()) {
              <a routerLink="/employees/new" class="contents">
                <kadr-button variant="primary" type="button">{{
                  'employees.addFirst' | transloco
                }}</kadr-button>
              </a>
            }
          </kadr-empty-state>
        } @else {
          <div class="hidden overflow-x-auto rounded-lg border border-line bg-surface md:block">
            <table class="w-full text-start text-sm">
              <thead class="border-b border-line bg-subtle text-xs text-fg-2">
                <tr>
                  <th class="px-3 py-3 font-medium">{{ 'employees.colName' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.colNumber' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.colJob' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.colDepartment' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.colBranch' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.colManager' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.colStatus' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of items(); track row.id) {
                  <tr class="border-b border-line hover:bg-hover">
                    <td class="px-3 py-3">
                      <div class="flex items-center gap-3">
                        <kadr-avatar [name]="row.fullNameAr" size="sm" [ariaLabel]="row.fullNameAr" />
                        <div class="min-w-0">
                          <a
                            [routerLink]="['/employees', row.id]"
                            class="font-medium text-accent hover:underline"
                          >
                            {{ displayName(row) }}
                          </a>
                          @if (row.email) {
                            <p class="text-xs text-fg-muted"><bdi>{{ row.email }}</bdi></p>
                          }
                        </div>
                      </div>
                    </td>
                    <td class="px-3 py-3 tabular-nums text-fg-2"><bdi>{{ row.employeeNumber }}</bdi></td>
                    <td class="px-3 py-3 text-fg">{{ jobName(row) }}</td>
                    <td class="px-3 py-3 text-fg">{{ deptName(row) }}</td>
                    <td class="px-3 py-3 text-fg">{{ row.branchName || '—' }}</td>
                    <td class="px-3 py-3 text-fg">{{ row.managerNameAr || '—' }}</td>
                    <td class="px-3 py-3">
                      <kadr-badge [tone]="statusTone(row.status)" [label]="statusLabel(row.status)" />
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <ul class="flex flex-col gap-3 md:hidden">
            @for (row of items(); track row.id) {
              <li class="rounded-lg border border-line bg-surface p-4">
                <a [routerLink]="['/employees', row.id]" class="font-medium text-accent">{{
                  displayName(row)
                }}</a>
                <p class="mt-1 text-xs text-fg-muted"><bdi>{{ row.employeeNumber }}</bdi></p>
                <div class="mt-2">
                  <kadr-badge [tone]="statusTone(row.status)" [label]="statusLabel(row.status)" />
                </div>
              </li>
            }
          </ul>

          <div class="flex items-center justify-between gap-3 text-sm text-fg-2">
            <p>
              {{
                'employees.pageInfo'
                  | transloco: { from: pageFrom(), to: pageTo(), total: totalCount() }
              }}
            </p>
            <div class="flex gap-2">
              <kadr-button
                variant="secondary"
                type="button"
                [disabled]="page() <= 1"
                (click)="goPage(page() - 1)"
              >
                {{ 'employees.prev' | transloco }}
              </kadr-button>
              <kadr-button
                variant="secondary"
                type="button"
                [disabled]="pageTo() >= totalCount()"
                (click)="goPage(page() + 1)"
              >
                {{ 'employees.next' | transloco }}
              </kadr-button>
            </div>
          </div>
        }
      }
    </div>
  `,
})
export class EmployeesListPage implements OnInit {
  private readonly api = inject(EmployeesApi);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(TranslocoService);
  private readonly route = inject(ActivatedRoute);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<EmployeeListItem[]>([]);
  protected readonly summary = signal<EmployeeSummary | null>(null);
  protected readonly q = signal('');
  protected readonly status = signal('');
  protected readonly departmentId = signal('');
  protected readonly branchId = signal('');
  protected readonly jobTitleId = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = 25;
  protected readonly totalCount = signal(0);
  protected readonly lookups = signal<OrgLookups | null>(null);

  protected readonly departmentOptions = computed((): SelectOption[] => {
    const all = { value: '', label: this.i18n.translate('employees.filterAll') };
    const lang = this.i18n.getActiveLang();
    const items =
      this.lookups()?.departments.map((d) => ({
        value: d.id,
        label: lang === 'en' ? d.nameEn || d.nameAr : d.nameAr,
      })) ?? [];
    return [all, ...items];
  });
  protected readonly branchOptions = computed((): SelectOption[] => {
    const all = { value: '', label: this.i18n.translate('employees.filterAll') };
    const items =
      this.lookups()?.branches.map((b) => ({
        value: b.id,
        label: b.nameAr,
      })) ?? [];
    return [all, ...items];
  });
  protected readonly jobOptions = computed((): SelectOption[] => {
    const all = { value: '', label: this.i18n.translate('employees.filterAll') };
    const lang = this.i18n.getActiveLang();
    const items =
      this.lookups()?.jobTitles.map((j) => ({
        value: j.id,
        label: lang === 'en' ? j.nameEn || j.nameAr : j.nameAr,
      })) ?? [];
    return [all, ...items];
  });

  protected readonly statusTabs = [
    { value: '', labelKey: 'employees.status.all' },
    { value: 'Active', labelKey: 'employees.status.active' },
    { value: 'Probation', labelKey: 'employees.status.probation' },
    { value: 'OnLeave', labelKey: 'employees.status.onLeave' },
    { value: 'Suspended', labelKey: 'employees.status.suspended' },
    { value: 'Terminated', labelKey: 'employees.status.terminated' },
  ] as const;

  protected readonly pageFrom = computed(() =>
    this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize + 1,
  );
  protected readonly pageTo = computed(() =>
    Math.min(this.page() * this.pageSize, this.totalCount()),
  );
  protected readonly capacityPct = computed(() => {
    const s = this.summary();
    if (!s?.employeeCap) return 0;
    return Math.min(100, Math.round((s.activeForCapacity / s.employeeCap) * 100));
  });
  protected readonly atCap = computed(() => {
    const s = this.summary();
    return !!s?.employeeCap && s.activeForCapacity >= s.employeeCap;
  });

  ngOnInit(): void {
    const dept = this.route.snapshot.queryParamMap.get('departmentId');
    if (dept) this.departmentId.set(dept);
    void this.api.orgLookups().then((l) => this.lookups.set(l)).catch(() => undefined);
    void this.reload();
  }

  clearFilters(): void {
    this.q.set('');
    this.departmentId.set('');
    this.branchId.set('');
    this.jobTitleId.set('');
    this.page.set(1);
    void this.reload();
  }

  canCreate(): boolean {
    return this.auth.hasPermission(PermissionCodes.EmployeesCreate);
  }

  canImport(): boolean {
    return this.auth.hasPermission(PermissionCodes.EmployeesImport);
  }

  canImportSalaries(): boolean {
    return this.auth.hasPermission(PermissionCodes.SalaryManage);
  }

  setStatus(value: string): void {
    this.status.set(value);
    this.page.set(1);
    void this.reload();
  }

  goPage(p: number): void {
    this.page.set(p);
    void this.reload();
  }

  reload(): void {
    void this.load();
  }

  displayName(row: EmployeeListItem): string {
    return this.i18n.getActiveLang() === 'en' && row.fullNameEn ? row.fullNameEn : row.fullNameAr;
  }

  jobName(row: EmployeeListItem): string {
    return (this.i18n.getActiveLang() === 'ar' ? row.jobTitleNameAr : row.jobTitleNameEn) || '—';
  }

  deptName(row: EmployeeListItem): string {
    return (this.i18n.getActiveLang() === 'ar' ? row.departmentNameAr : row.departmentNameEn) || '—';
  }

  statusTone(status: string): BadgeTone {
    switch (status) {
      case 'Active':
        return 'success';
      case 'Probation':
        return 'info';
      case 'OnLeave':
        return 'neutral';
      case 'Suspended':
        return 'warning';
      default:
        return 'neutral';
    }
  }

  statusLabel(status: string): string {
    const key = `employees.status.${status.charAt(0).toLowerCase()}${status.slice(1)}`;
    const mapped: Record<string, string> = {
      Active: 'employees.status.active',
      Probation: 'employees.status.probation',
      OnLeave: 'employees.status.onLeave',
      Suspended: 'employees.status.suspended',
      Terminated: 'employees.status.terminated',
    };
    return this.i18n.translate(mapped[status] ?? key);
  }

  statusCount(s: EmployeeSummary, value: string): number {
    switch (value) {
      case 'Active':
        return s.statusCounts.active;
      case 'Probation':
        return s.statusCounts.probation;
      case 'OnLeave':
        return s.statusCounts.onLeave;
      case 'Suspended':
        return s.statusCounts.suspended;
      case 'Terminated':
        return s.statusCounts.terminated;
      default:
        return s.statusCounts.all;
    }
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      const result = await this.api.list({
        page: this.page(),
        pageSize: this.pageSize,
        q: this.q().trim() || undefined,
        status: this.status() || undefined,
        departmentId: this.departmentId() || undefined,
        branchId: this.branchId() || undefined,
        jobTitleId: this.jobTitleId() || undefined,
      });
      this.items.set(result.items);
      this.summary.set(result.summary);
      this.totalCount.set(result.totalCount);
      this.state.set(result.items.length === 0 ? 'empty' : 'ready');
    } catch (err: unknown) {
      const code = (err as { error?: { code?: string } })?.error?.code;
      this.state.set(code === 'plan.feature_not_available' ? 'locked' : 'error');
    }
  }
}
