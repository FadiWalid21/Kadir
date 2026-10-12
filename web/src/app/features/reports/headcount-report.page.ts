import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { HeadcountReport, HeadcountRow, ReportsApi } from '../../core/api/reports.api';
import { LookupsApi } from '../../core/api/lookups.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import {
  ComboboxComponent,
  ComboboxOption,
  ComboboxSearchFn,
} from '../../shared/ui/combobox/combobox.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SelectComponent, SelectOption } from '../../shared/ui/select/select.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error' | 'empty' | 'forbidden';

@Component({
  selector: 'kadr-headcount-report-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    ComboboxComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SelectComponent,
    SkeletonComponent,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <nav class="text-sm text-fg-2" aria-label="Breadcrumb">
        <ol class="flex flex-wrap items-center gap-1">
          <li>
            <a routerLink="/reports" class="hover:text-fg focus-visible:outline-none">{{
              'reports.hub.title' | transloco
            }}</a>
          </li>
          <li aria-hidden="true">/</li>
          <li class="text-fg">{{ 'reports.headcount.title' | transloco }}</li>
        </ol>
      </nav>

      <header class="flex flex-wrap items-start justify-between gap-3">
        <div class="text-start">
          <h1 class="text-2xl font-bold text-fg">{{ 'reports.headcount.title' | transloco }}</h1>
          <p class="mt-1 text-sm text-fg-2">{{ 'reports.headcount.subtitle' | transloco }}</p>
        </div>
        @if (canExport()) {
          <div class="flex flex-wrap gap-2">
            <kadr-button
              variant="secondary"
              type="button"
              [disabled]="exporting()"
              (click)="exportFile('xlsx')"
            >
              {{ 'reports.export.xlsx' | transloco }}
            </kadr-button>
            <kadr-button
              variant="secondary"
              type="button"
              [disabled]="exporting()"
              (click)="exportFile('pdf')"
            >
              {{ 'reports.export.pdf' | transloco }}
            </kadr-button>
          </div>
        }
      </header>

      <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <label class="grid gap-2 text-sm">
          <span class="font-medium text-fg">{{ 'reports.headcount.asOf' | transloco }}</span>
          <input
            type="date"
            class="h-control rounded-md border border-line-strong bg-surface px-3 text-base text-fg"
            [ngModel]="asOf()"
            (ngModelChange)="onAsOf($event)"
          />
        </label>
        <kadr-combobox
          [label]="'reports.filters.branch' | transloco"
          [searchFn]="branchSearch"
          [value]="branchId()"
          [displayName]="branchName()"
          (selectionChange)="onBranch($event)"
        />
        <kadr-combobox
          [label]="'reports.filters.department' | transloco"
          [searchFn]="departmentSearch"
          [value]="departmentId()"
          [displayName]="departmentName()"
          (selectionChange)="onDepartment($event)"
        />
        <kadr-select
          [label]="'reports.filters.status' | transloco"
          [options]="statusOptions()"
          [value]="status()"
          (valueChange)="onStatus($event)"
        />
        <kadr-select
          [label]="'reports.filters.contractType' | transloco"
          [options]="contractOptions()"
          [value]="contractType()"
          (valueChange)="onContract($event)"
        />
      </div>

      @if (state() === 'loading') {
        <kadr-skeleton height="12rem" />
        <kadr-skeleton height="20rem" />
      } @else if (state() === 'forbidden') {
        <kadr-empty-state
          [title]="'reports.forbiddenTitle' | transloco"
          [description]="'reports.forbiddenBody' | transloco"
        />
      } @else if (state() === 'error') {
        <kadr-network-error
          [title]="'reports.headcount.loadErrorTitle' | transloco"
          [description]="'reports.headcount.loadErrorBody' | transloco"
          (retry)="reload()"
        />
      } @else if (report(); as r) {
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
          <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-muted">{{ 'reports.headcount.tileTotal' | transloco }}</p>
            <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ r.summary.total }}</p>
          </article>
          @for (s of r.summary.byStatus; track s.status) {
            <article class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
              <p class="truncate text-xs text-fg-muted" [attr.title]="statusLabel(s.status)">
                {{ statusLabel(s.status) }}
              </p>
              <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ s.count }}</p>
            </article>
          }
        </div>

        @if (state() === 'empty') {
          <kadr-empty-state
            [title]="'reports.headcount.emptyTitle' | transloco"
            [description]="'reports.headcount.emptyBody' | transloco"
          />
        } @else {
          <div class="overflow-x-auto rounded-lg border border-line bg-surface">
            <table class="min-w-full text-sm">
              <thead class="sticky top-0 bg-subtle text-start">
                <tr class="border-b border-line text-fg-2">
                  <th class="px-3 py-2 font-medium">{{ 'reports.headcount.colNumber' | transloco }}</th>
                  <th class="px-3 py-2 font-medium">{{ 'reports.headcount.colName' | transloco }}</th>
                  <th class="px-3 py-2 font-medium">{{ 'reports.headcount.colBranch' | transloco }}</th>
                  <th class="px-3 py-2 font-medium">{{ 'reports.headcount.colDepartment' | transloco }}</th>
                  <th class="px-3 py-2 font-medium">{{ 'reports.headcount.colStatus' | transloco }}</th>
                  <th class="px-3 py-2 font-medium">{{ 'reports.headcount.colContract' | transloco }}</th>
                  <th class="px-3 py-2 font-medium">{{ 'reports.headcount.colHire' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of r.items; track row.employeeId) {
                  <tr class="border-b border-line last:border-b-0">
                    <td class="px-3 py-2 tabular-nums text-fg">{{ row.employeeNumber }}</td>
                    <td class="max-w-48 truncate px-3 py-2 text-fg" [attr.title]="displayName(row)">
                      {{ displayName(row) }}
                    </td>
                    <td class="max-w-40 truncate px-3 py-2 text-fg" [attr.title]="branchLabel(row)">
                      {{ branchLabel(row) }}
                    </td>
                    <td class="max-w-40 truncate px-3 py-2 text-fg" [attr.title]="deptLabel(row)">
                      {{ deptLabel(row) }}
                    </td>
                    <td class="px-3 py-2 text-fg">{{ statusLabel(row.status) }}</td>
                    <td class="px-3 py-2 text-fg">{{ contractLabel(row.contractType) }}</td>
                    <td class="px-3 py-2 tabular-nums text-fg">{{ row.hireDate }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <div class="flex flex-wrap items-center justify-between gap-2 text-sm text-fg-2">
            <p>
              {{
                'reports.paging'
                  | transloco
                    : {
                        page: r.page,
                        pages: totalPages(),
                        total: r.totalCount,
                      }
              }}
            </p>
            <div class="flex gap-2">
              <kadr-button
                variant="ghost"
                type="button"
                [disabled]="r.page <= 1"
                (click)="setPage(r.page - 1)"
              >
                {{ 'employees.prev' | transloco }}
              </kadr-button>
              <kadr-button
                variant="ghost"
                type="button"
                [disabled]="r.page >= totalPages()"
                (click)="setPage(r.page + 1)"
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
export class HeadcountReportPage implements OnInit {
  private readonly api = inject(ReportsApi);
  private readonly lookups = inject(LookupsApi);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly report = signal<HeadcountReport | null>(null);
  protected readonly exporting = signal(false);

  protected readonly asOf = signal(todayIso());
  protected readonly branchId = signal('');
  protected readonly branchName = signal('');
  protected readonly departmentId = signal('');
  protected readonly departmentName = signal('');
  protected readonly status = signal('');
  protected readonly contractType = signal('');
  protected readonly page = signal(1);

  protected readonly canExport = computed(() =>
    this.auth.hasPermission(PermissionCodes.ReportsExport),
  );

  protected readonly statusOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('reports.filters.all') },
    ...['Active', 'Probation', 'OnLeave', 'Suspended', 'Terminated'].map((value) => ({
      value,
      label: this.statusLabel(value),
    })),
  ]);

  protected readonly contractOptions = computed<SelectOption[]>(() => [
    { value: '', label: this.i18n.translate('reports.filters.all') },
    ...['Unlimited', 'FixedTerm', 'Other'].map((value) => ({
      value,
      label: this.contractLabel(value),
    })),
  ]);

  protected readonly branchSearch: ComboboxSearchFn = async (q) => {
    const page = await this.lookups.searchBranches(q, 1, 20);
    return page.items.map((i) => ({ id: i.id, name: i.name }));
  };

  protected readonly departmentSearch: ComboboxSearchFn = async (q) => {
    const page = await this.lookups.searchDepartments(q, 1, 20);
    return page.items.map((i) => ({ id: i.id, name: i.name }));
  };

  ngOnInit(): void {
    document.title = this.i18n.translate('reports.headcount.title');
    const q = this.route.snapshot.queryParamMap;
    this.asOf.set(q.get('asOf') || todayIso());
    this.branchId.set(q.get('branchId') || '');
    this.branchName.set(q.get('branchName') || '');
    this.departmentId.set(q.get('departmentId') || '');
    this.departmentName.set(q.get('departmentName') || '');
    this.status.set(q.get('status') || '');
    this.contractType.set(q.get('contractType') || '');
    this.page.set(Math.max(1, Number(q.get('page') || '1') || 1));
    void this.reload();
  }

  protected totalPages(): number {
    const r = this.report();
    if (!r || r.pageSize <= 0) {
      return 1;
    }
    return Math.max(1, Math.ceil(r.totalCount / r.pageSize));
  }

  protected displayName(row: HeadcountRow): string {
    return this.i18n.getActiveLang() === 'en' ? row.fullNameEn || row.fullNameAr : row.fullNameAr;
  }

  protected branchLabel(row: HeadcountRow): string {
    return this.i18n.getActiveLang() === 'en' ? row.branchNameEn || row.branchNameAr : row.branchNameAr;
  }

  protected deptLabel(row: HeadcountRow): string {
    const ar = row.departmentNameAr;
    const en = row.departmentNameEn;
    if (!ar && !en) {
      return this.i18n.translate('reports.unassigned');
    }
    return this.i18n.getActiveLang() === 'en' ? en || ar || '' : ar || en || '';
  }

  protected statusLabel(status: string): string {
    const key =
      status === 'OnLeave'
        ? 'onLeave'
        : status.charAt(0).toLowerCase() + status.slice(1);
    return this.i18n.translate(`employees.status.${key}`);
  }

  protected contractLabel(type: string): string {
    const map: Record<string, string> = {
      Unlimited: 'employees.contractUnlimited',
      FixedTerm: 'employees.contractFixed',
      Other: 'employees.contractOther',
    };
    return this.i18n.translate(map[type] ?? 'employees.contractOther');
  }

  protected onAsOf(value: string): void {
    this.asOf.set(value);
    this.page.set(1);
    void this.reload(true);
  }

  protected onBranch(opt: ComboboxOption | null): void {
    this.branchId.set(opt?.id ?? '');
    this.branchName.set(opt?.name ?? '');
    this.page.set(1);
    void this.reload(true);
  }

  protected onDepartment(opt: ComboboxOption | null): void {
    this.departmentId.set(opt?.id ?? '');
    this.departmentName.set(opt?.name ?? '');
    this.page.set(1);
    void this.reload(true);
  }

  protected onStatus(value: string): void {
    this.status.set(value);
    this.page.set(1);
    void this.reload(true);
  }

  protected onContract(value: string): void {
    this.contractType.set(value);
    this.page.set(1);
    void this.reload(true);
  }

  protected setPage(page: number): void {
    this.page.set(page);
    void this.reload(true);
  }

  protected async reload(syncUrl = false): Promise<void> {
    this.state.set('loading');
    if (syncUrl) {
      await this.router.navigate([], {
        relativeTo: this.route,
        queryParams: {
          asOf: this.asOf() || null,
          branchId: this.branchId() || null,
          branchName: this.branchName() || null,
          departmentId: this.departmentId() || null,
          departmentName: this.departmentName() || null,
          status: this.status() || null,
          contractType: this.contractType() || null,
          page: this.page() > 1 ? this.page() : null,
        },
        queryParamsHandling: '',
        replaceUrl: true,
      });
    }

    try {
      const data = await this.api.headcount({
        asOf: this.asOf() || undefined,
        branchId: this.branchId() || undefined,
        departmentId: this.departmentId() || undefined,
        status: this.status() ? [this.status()] : undefined,
        contractType: this.contractType() ? [this.contractType()] : undefined,
        page: this.page(),
        pageSize: 20,
      });
      this.report.set(data);
      this.state.set(data.totalCount === 0 ? 'empty' : 'ready');
    } catch (err: unknown) {
      const status = (err as { status?: number })?.status;
      this.state.set(status === 403 ? 'forbidden' : 'error');
    }
  }

  protected async exportFile(format: 'xlsx' | 'pdf'): Promise<void> {
    this.exporting.set(true);
    try {
      const blob = await this.api.exportHeadcount(format, {
        asOf: this.asOf() || undefined,
        branchId: this.branchId() || undefined,
        departmentId: this.departmentId() || undefined,
        status: this.status() ? [this.status()] : undefined,
        contractType: this.contractType() ? [this.contractType()] : undefined,
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `headcount.${format}`;
      a.click();
      URL.revokeObjectURL(url);
      this.toast.show(this.i18n.translate('reports.export.done'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('reports.export.failed'), 'danger');
    } finally {
      this.exporting.set(false);
    }
  }
}

function todayIso(): string {
  const d = new Date();
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}
