import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnChanges,
  signal,
  SimpleChanges,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  EmployeeSalariesApi,
  EmployeeSalary,
  EmployeeSalaryListItem,
} from '../../core/api/employee-salaries.api';
import { PayComponentsApi } from '../../core/api/pay-components.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { EmployeeInsuranceSectionComponent } from './employee-insurance-section.component';

type PanelState = 'loading' | 'ready' | 'error';

interface DraftLine {
  payComponentId: string;
  amount: string;
}

@Component({
  selector: 'kadr-employee-salary-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    SkeletonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    DrawerComponent,
    EmployeeInsuranceSectionComponent,
  ],
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div class="text-start">
          <h2 class="text-sm font-bold text-fg">{{ 'employees.salary.currentTitle' | transloco }}</h2>
          <p class="mt-1 text-xs text-fg-muted">{{ 'employees.salary.subtitle' | transloco }}</p>
        </div>
        @if (canMutate()) {
          <kadr-button variant="primary" type="button" (click)="openCreate()">
            {{ 'employees.salary.add' | transloco }}
          </kadr-button>
        }
      </div>

      @if (state() === 'loading') {
        <kadr-skeleton height="10rem" />
      } @else if (state() === 'error') {
        <kadr-network-error
          [title]="'employees.salary.loadErrorTitle' | transloco"
          [description]="'employees.salary.loadErrorBody' | transloco"
          (retry)="reload()"
        />
      } @else {
        @if (current(); as cur) {
          <section class="rounded-lg border border-line bg-surface p-4 text-start">
            <p class="text-sm font-medium text-fg">
              {{ formatMoney(cur.basicAmount, cur.currency) }}
              <span class="text-fg-2">· {{ 'employees.salary.basic' | transloco }}</span>
            </p>
            <p class="mt-1 text-xs text-fg-muted">
              {{ 'employees.salary.effectiveFrom' | transloco }}:
              <span class="tabular-nums">{{ cur.effectiveFrom }}</span>
              @if (cur.effectiveTo) {
                — {{ cur.effectiveTo }}
              }
            </p>
            @if (cur.components.length > 0) {
              <ul class="mt-3 flex flex-col gap-1 text-sm text-fg-2">
                @for (line of cur.components; track line.id) {
                  <li class="flex justify-between gap-2">
                    <span>{{ componentName(line.payComponentNameAr, line.payComponentNameEn) }}</span>
                    <span class="tabular-nums">{{ formatMoney(line.amountOrRate, cur.currency) }}</span>
                  </li>
                }
              </ul>
            }
          </section>
        } @else {
          <kadr-empty-state
            [title]="'employees.salary.emptyTitle' | transloco"
            [description]="'employees.salary.emptyBody' | transloco"
          >
            @if (canMutate()) {
              <kadr-button variant="primary" type="button" (click)="openCreate()">
                {{ 'employees.salary.add' | transloco }}
              </kadr-button>
            }
          </kadr-empty-state>
        }

        <section class="flex flex-col gap-2 text-start">
          <h3 class="text-sm font-bold text-fg">{{ 'employees.salary.historyTitle' | transloco }}</h3>
          @if (history().length === 0) {
            <p class="text-sm text-fg-2">{{ 'employees.salary.historyEmpty' | transloco }}</p>
          } @else {
            <ul class="flex flex-col gap-2">
              @for (row of history(); track row.id) {
                <li class="rounded-lg border border-line bg-surface p-3 text-sm">
                  <div class="flex flex-wrap items-baseline justify-between gap-2">
                    <span class="font-medium text-fg tabular-nums">
                      {{ formatMoney(row.basicAmount, row.currency) }}
                    </span>
                    <span class="text-xs text-fg-muted tabular-nums">
                      {{ row.effectiveFrom }}
                      @if (row.effectiveTo) {
                        — {{ row.effectiveTo }}
                      } @else {
                        — {{ 'employees.salary.openPeriod' | transloco }}
                      }
                    </span>
                  </div>
                  <p class="mt-1 text-xs text-fg-2">{{ row.changeReason }}</p>
                </li>
              }
            </ul>
          }
        </section>

        <kadr-employee-insurance-section [employeeId]="employeeId()" />
      }
    </div>

    <kadr-drawer
      [open]="drawerOpen()"
      [title]="'employees.salary.addTitle' | transloco"
      [closeLabel]="'ui.cancel' | transloco"
      [hasFooter]="false"
      (closed)="closeDrawer()"
    >
      <form class="flex flex-col gap-4" (ngSubmit)="onSave()">
        <kadr-input
          [label]="'employees.salary.effectiveFrom' | transloco"
          type="date"
          [value]="draftFrom()"
          (valueChange)="draftFrom.set($event)"
        />
        <kadr-input
          [label]="'employees.salary.currency' | transloco"
          [value]="draftCurrency()"
          (valueChange)="draftCurrency.set($event)"
        />
        <kadr-input
          [label]="'employees.salary.basicAmount' | transloco"
          type="number"
          [value]="draftBasic()"
          (valueChange)="draftBasic.set($event)"
        />
        <kadr-input
          [label]="'employees.salary.reason' | transloco"
          [value]="draftReason()"
          (valueChange)="draftReason.set($event)"
        />

        <div class="flex flex-col gap-2">
          <div class="flex items-center justify-between gap-2">
            <span class="text-sm font-medium text-fg">{{ 'employees.salary.components' | transloco }}</span>
            <kadr-button variant="secondary" type="button" size="sm" (click)="addLine()">
              {{ 'employees.salary.addLine' | transloco }}
            </kadr-button>
          </div>
          @for (line of draftLines(); track $index; let i = $index) {
            <div class="flex flex-col gap-2 rounded-md border border-line p-3 sm:flex-row sm:items-end">
              <label class="flex min-w-0 flex-1 flex-col gap-1 text-sm">
                <span class="text-fg-2">{{ 'employees.salary.component' | transloco }}</span>
                <select
                  class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
                  [ngModel]="line.payComponentId"
                  (ngModelChange)="setLineComponent(i, $event)"
                  [name]="'comp' + i"
                >
                  <option value="">{{ 'employees.salary.selectComponent' | transloco }}</option>
                  @for (opt of componentOptions(); track opt.id) {
                    <option [value]="opt.id">{{ opt.label }}</option>
                  }
                </select>
              </label>
              <kadr-input
                class="sm:w-32"
                [label]="'employees.salary.amount' | transloco"
                type="number"
                [value]="line.amount"
                (valueChange)="setLineAmount(i, $event)"
              />
              <kadr-button variant="ghost" type="button" size="sm" (click)="removeLine(i)">
                {{ 'ui.delete' | transloco }}
              </kadr-button>
            </div>
          }
        </div>

        <div class="mt-2 flex flex-wrap justify-end gap-2">
          <kadr-button variant="secondary" type="button" (click)="closeDrawer()">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
          <kadr-button variant="primary" type="submit" [loading]="busy()" [disabled]="!canMutate()">
            {{ 'ui.save' | transloco }}
          </kadr-button>
        </div>
      </form>
    </kadr-drawer>
  `,
})
export class EmployeeSalaryPanelComponent implements OnChanges {
  private readonly api = inject(EmployeeSalariesApi);
  private readonly payComponentsApi = inject(PayComponentsApi);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(TranslocoService);

  readonly employeeId = input.required<string>();

  protected readonly state = signal<PanelState>('loading');
  protected readonly current = signal<EmployeeSalary | null>(null);
  protected readonly history = signal<EmployeeSalaryListItem[]>([]);
  protected readonly busy = signal(false);
  protected readonly drawerOpen = signal(false);
  protected readonly draftFrom = signal('');
  protected readonly draftCurrency = signal('EGP');
  protected readonly draftBasic = signal('');
  protected readonly draftReason = signal('');
  protected readonly draftLines = signal<DraftLine[]>([]);
  protected readonly componentOptions = signal<{ id: string; label: string }[]>([]);

  protected readonly canMutate = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.SalaryManage) && !this.subscription.readOnly(),
  );

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['employeeId']) {
      void this.reload();
    }
  }

  reload(): void {
    void this.load();
  }

  formatMoney(amount: number, currency: string): string {
    return `${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;
  }

  componentName(ar: string, en: string): string {
    return this.i18n.getActiveLang() === 'en' ? en || ar : ar;
  }

  openCreate(): void {
    const today = new Date().toISOString().slice(0, 10);
    this.draftFrom.set(today);
    this.draftCurrency.set('EGP');
    this.draftBasic.set('');
    this.draftReason.set('');
    this.draftLines.set([]);
    void this.loadComponentOptions();
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  addLine(): void {
    this.draftLines.update((rows) => [...rows, { payComponentId: '', amount: '' }]);
  }

  removeLine(index: number): void {
    this.draftLines.update((rows) => rows.filter((_, i) => i !== index));
  }

  setLineComponent(index: number, id: string): void {
    this.draftLines.update((rows) =>
      rows.map((r, i) => (i === index ? { ...r, payComponentId: id } : r)),
    );
  }

  setLineAmount(index: number, amount: string): void {
    this.draftLines.update((rows) =>
      rows.map((r, i) => (i === index ? { ...r, amount } : r)),
    );
  }

  async onSave(): Promise<void> {
    const employeeId = this.employeeId();
    const basic = Number(this.draftBasic());
    const reason = this.draftReason().trim();
    const from = this.draftFrom().trim();
    if (!from || !reason || Number.isNaN(basic) || basic < 0) {
      this.toast.show(this.i18n.translate('employees.salary.toastValidation'), 'danger');
      return;
    }

    const components = this.draftLines()
      .filter((l) => l.payComponentId)
      .map((l) => ({
        payComponentId: l.payComponentId,
        amountOrRate: Number(l.amount),
      }));
    if (components.some((c) => Number.isNaN(c.amountOrRate) || c.amountOrRate < 0)) {
      this.toast.show(this.i18n.translate('employees.salary.toastValidation'), 'danger');
      return;
    }

    this.busy.set(true);
    try {
      await this.api.create(employeeId, {
        effectiveFrom: from,
        currency: this.draftCurrency().trim() || 'EGP',
        basicAmount: basic,
        changeReason: reason,
        components: components.length > 0 ? components : null,
      });
      this.closeDrawer();
      this.toast.show(this.i18n.translate('employees.salary.toastSaved'), 'success');
      await this.load();
    } catch (err) {
      const code = problemDetailsFromError(err)?.code;
      const key =
        code === 'salary.effective_overlap'
          ? 'employees.salary.toastOverlap'
          : code === 'salary.invalid_component'
            ? 'employees.salary.toastInvalidComponent'
            : 'employees.salary.toastFail';
      this.toast.show(this.i18n.translate(key), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    const id = this.employeeId();
    if (!id) {
      return;
    }
    this.state.set('loading');
    try {
      const [current, page] = await Promise.all([
        this.api.getCurrent(id),
        this.api.list(id, 1, 100),
      ]);
      this.current.set(current);
      this.history.set(page.items);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  private async loadComponentOptions(): Promise<void> {
    try {
      const page = await this.payComponentsApi.list({ page: 1, pageSize: 100, isActive: true });
      const lang = this.i18n.getActiveLang();
      this.componentOptions.set(
        page.items
          .filter((c) => c.key !== 'basic')
          .map((c) => ({
            id: c.id,
            label: lang === 'en' ? c.nameEn : c.nameAr,
          })),
      );
    } catch {
      this.componentOptions.set([]);
    }
  }
}
