import { DecimalPipe } from '@angular/common';
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
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  EmployeeInsuranceApi,
  EmployeeInsuranceProfile,
} from '../../core/api/employee-insurance.api';
import {
  EmployeeSalariesApi,
  EmployeeSalary,
  EmployeeSalaryListItem,
} from '../../core/api/employee-salaries.api';
import {
  OvertimeMultipliers,
  OvertimeMultipliersApi,
} from '../../core/api/overtime-multipliers.api';
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

interface StructureLine {
  key: string;
  nameAr: string;
  nameEn: string;
  amount: number;
  pct: number;
  isTaxable: boolean;
  isInsurable: boolean;
  barClass: string;
}

const BAR_CLASSES = [
  'bg-accent',
  'bg-accent-soft',
  'bg-warning',
  'bg-success',
  'bg-fg-muted',
] as const;

@Component({
  selector: 'kadr-employee-salary-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    RouterLink,
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
    <div class="flex flex-col gap-5">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div class="text-start">
          <h2 class="text-lg font-bold text-fg">{{ 'employees.salary.structureTitle' | transloco }}</h2>
          <p class="mt-1 text-sm text-fg-2">{{ 'employees.salary.subtitle' | transloco }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          @if (canMutate()) {
            <a routerLink="/employees/salaries/import" class="contents">
              <kadr-button variant="secondary" type="button">{{
                'employees.salary.import' | transloco
              }}</kadr-button>
            </a>
            <kadr-button variant="primary" type="button" (click)="openCreate()">
              {{ 'employees.salary.editStructure' | transloco }}
            </kadr-button>
          }
        </div>
      </div>

      @if (state() === 'loading') {
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <kadr-skeleton height="6rem" />
          <kadr-skeleton height="6rem" />
        </div>
        <kadr-skeleton height="12rem" />
      } @else if (state() === 'error') {
        <kadr-network-error
          [title]="'employees.salary.loadErrorTitle' | transloco"
          [description]="'employees.salary.loadErrorBody' | transloco"
          (retry)="reload()"
        />
      } @else if (!current()) {
        <kadr-empty-state
          [title]="'employees.salary.emptyTitle' | transloco"
          [description]="'employees.salary.emptyBody' | transloco"
        >
          @if (canMutate()) {
            <div class="flex flex-wrap gap-2">
              <kadr-button variant="primary" type="button" (click)="openCreate()">
                {{ 'employees.salary.add' | transloco }}
              </kadr-button>
              <a routerLink="/employees/salaries/import" class="contents">
                <kadr-button variant="secondary" type="button">{{
                  'employees.salary.import' | transloco
                }}</kadr-button>
              </a>
            </div>
          }
        </kadr-empty-state>
        <kadr-employee-insurance-section [employeeId]="employeeId()" />
      } @else {
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <div class="flex items-center justify-between gap-2 text-fg-2">
              <span class="text-sm">{{ 'employees.salary.grossTile' | transloco }}</span>
            </div>
            <p class="mt-2 text-2xl font-bold tabular-nums text-fg">
              {{ formatMoney(gross(), current()!.currency) }}
            </p>
            <p class="mt-2 text-xs text-fg-muted">
              {{ 'employees.salary.perMonth' | transloco: { currency: current()!.currency } }}
              @if (grossDeltaPct(); as d) {
                <span class="ms-2" [class.text-success]="d > 0" [class.text-danger]="d < 0">
                  {{ formatDelta(d) }}
                </span>
              }
            </p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <div class="flex items-center justify-between gap-2 text-fg-2">
              <span class="text-sm">{{ 'employees.salary.insuredWageTile' | transloco }}</span>
            </div>
            <p class="mt-2 text-2xl font-bold tabular-nums text-fg">
              @if (insuredWage() != null) {
                {{ formatMoney(insuredWage()!, current()!.currency) }}
              } @else {
                —
              }
            </p>
            <p class="mt-2 text-xs text-fg-muted">{{ 'employees.salary.insuredWageHint' | transloco }}</p>
          </div>
        </div>

        <div class="grid grid-cols-1 gap-5 lg:grid-cols-12">
          <div class="flex flex-col gap-5 lg:col-span-7">
            <section class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm sm:p-5">
              <div class="mb-4 flex flex-wrap items-start justify-between gap-2">
                <div>
                  <h3 class="text-base font-bold text-fg">
                    {{ 'employees.salary.structureTitle' | transloco }}
                  </h3>
                  <p class="mt-0.5 text-xs text-fg-2">{{ 'employees.salary.structureSubtitle' | transloco }}</p>
                </div>
                <span
                  class="rounded-full bg-success-soft px-2.5 py-1 text-xs font-medium text-success"
                >
                  {{
                    'employees.salary.effectiveBadge'
                      | transloco: { date: current()!.effectiveFrom }
                  }}
                </span>
              </div>

              <div class="mb-4">
                <div class="mb-2 flex items-center justify-between gap-2 text-sm">
                  <span class="text-fg">{{ 'employees.salary.distribution' | transloco }}</span>
                  <span class="font-semibold tabular-nums text-accent">
                    {{ formatMoney(gross(), current()!.currency) }}
                  </span>
                </div>
                <div class="flex h-3 w-full overflow-hidden rounded-full bg-subtle">
                  @for (line of structureLines(); track line.key) {
                    <div
                      class="h-full"
                      [class]="line.barClass"
                      [style.width.%]="line.pct"
                      [attr.title]="componentName(line.nameAr, line.nameEn)"
                    ></div>
                  }
                </div>
                <div class="mt-2 flex flex-wrap gap-3 text-xs text-fg-2">
                  @for (line of structureLines(); track line.key) {
                    <span class="inline-flex items-center gap-1.5">
                      <span class="size-2.5 rounded-full" [class]="line.barClass"></span>
                      {{ componentName(line.nameAr, line.nameEn) }}
                      ({{ line.pct | number: '1.0-1' }}%)
                    </span>
                  }
                </div>
              </div>

              <div class="overflow-x-auto">
                <table class="w-full text-start text-sm">
                  <thead class="bg-subtle text-xs text-fg-2">
                    <tr>
                      <th class="px-3 py-2 font-medium">{{ 'employees.salary.colItem' | transloco }}</th>
                      <th class="px-3 py-2 font-medium">{{ 'employees.salary.colClass' | transloco }}</th>
                      <th class="px-3 py-2 font-medium">{{ 'employees.salary.colPct' | transloco }}</th>
                      <th class="px-3 py-2 font-medium">{{ 'employees.salary.colAmount' | transloco }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (line of structureLines(); track line.key) {
                      <tr class="border-t border-line">
                        <td class="px-3 py-3 font-medium text-fg">
                          {{ componentName(line.nameAr, line.nameEn) }}
                        </td>
                        <td class="px-3 py-3">
                          <span class="rounded bg-subtle px-2 py-0.5 text-xs text-fg-2">
                            {{ classificationLabel(line.isTaxable, line.isInsurable) }}
                          </span>
                        </td>
                        <td class="px-3 py-3 tabular-nums text-fg-2">
                          {{ line.pct | number: '1.0-1' }}%
                        </td>
                        <td class="px-3 py-3 font-semibold tabular-nums text-fg">
                          {{ formatMoney(line.amount, current()!.currency) }}
                        </td>
                      </tr>
                    }
                  </tbody>
                  <tfoot>
                    <tr class="border-t border-line bg-subtle font-bold text-fg">
                      <td class="px-3 py-3" colspan="2">
                        {{ 'employees.salary.grossTotal' | transloco }}
                      </td>
                      <td class="px-3 py-3 tabular-nums">100%</td>
                      <td class="px-3 py-3 tabular-nums text-accent">
                        {{ formatMoney(gross(), current()!.currency) }}
                      </td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            </section>

            @if (ot(); as multipliers) {
              <section class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
                <h3 class="mb-3 flex items-center gap-2 text-base font-bold text-fg">
                  {{ 'employees.salary.otTitle' | transloco }}
                </h3>
                <div class="grid grid-cols-1 gap-2 sm:grid-cols-3">
                  <div class="rounded-lg bg-subtle p-3">
                    <div class="text-xs text-fg-2">{{ 'employees.salary.otDay' | transloco }}</div>
                    <div class="mt-1 text-lg font-bold tabular-nums text-fg">
                      {{ formatMultiplierPct(multipliers.day) }}
                    </div>
                  </div>
                  <div class="rounded-lg bg-subtle p-3">
                    <div class="text-xs text-fg-2">{{ 'employees.salary.otNight' | transloco }}</div>
                    <div class="mt-1 text-lg font-bold tabular-nums text-fg">
                      {{ formatMultiplierPct(multipliers.night) }}
                    </div>
                  </div>
                  <div class="rounded-lg bg-subtle p-3">
                    <div class="text-xs text-fg-2">{{ 'employees.salary.otHoliday' | transloco }}</div>
                    <div class="mt-1 text-lg font-bold tabular-nums text-fg">
                      {{ formatMultiplierPct(multipliers.holiday) }}
                    </div>
                  </div>
                </div>
              </section>
            }
          </div>

          <div class="lg:col-span-5">
            <kadr-employee-insurance-section [employeeId]="employeeId()" />
          </div>
        </div>

        <section class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm sm:p-5">
          <h3 class="text-base font-bold text-fg">{{ 'employees.salary.historyTitle' | transloco }}</h3>
          <p class="mt-0.5 text-xs text-fg-2">{{ 'employees.salary.historySubtitle' | transloco }}</p>

          @if (history().length === 0) {
            <p class="mt-3 text-sm text-fg-2">{{ 'employees.salary.historyEmpty' | transloco }}</p>
          } @else {
            <div class="mt-4 overflow-x-auto">
              <table class="w-full text-start text-sm">
                <thead class="bg-subtle text-xs text-fg-2">
                  <tr>
                    <th class="px-3 py-2 font-medium">{{ 'employees.salary.colEffective' | transloco }}</th>
                    <th class="px-3 py-2 font-medium">{{ 'employees.salary.basic' | transloco }}</th>
                    <th class="px-3 py-2 font-medium">{{ 'employees.salary.colGross' | transloco }}</th>
                    <th class="px-3 py-2 font-medium">{{ 'employees.salary.colChangePct' | transloco }}</th>
                    <th class="px-3 py-2 font-medium">{{ 'employees.salary.reason' | transloco }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of historyRows(); track row.id) {
                    <tr
                      class="border-t border-line"
                      [class.bg-accent-soft]="row.isCurrent"
                    >
                      <td class="px-3 py-3">
                        <div class="font-medium tabular-nums text-fg">{{ row.effectiveFrom }}</div>
                        <span
                          class="mt-1 inline-block rounded px-1.5 py-0.5 text-xs"
                          [class.bg-success-soft]="row.isCurrent"
                          [class.text-success]="row.isCurrent"
                          [class.bg-subtle]="!row.isCurrent"
                          [class.text-fg-2]="!row.isCurrent"
                        >
                          {{
                            (row.isCurrent
                              ? 'employees.salary.statusCurrent'
                              : 'employees.salary.statusArchived'
                            ) | transloco
                          }}
                        </span>
                      </td>
                      <td class="px-3 py-3 font-semibold tabular-nums text-fg">
                        {{ formatMoney(row.basicAmount, row.currency) }}
                      </td>
                      <td class="px-3 py-3 font-bold tabular-nums text-fg">
                        {{ formatMoney(row.grossAmount, row.currency) }}
                      </td>
                      <td class="px-3 py-3 tabular-nums">
                        @if (row.changePct == null) {
                          <span class="text-fg-2">—</span>
                        } @else {
                          <span
                            class="inline-flex items-center rounded px-2 py-0.5 text-xs font-medium"
                            [class.bg-success-soft]="row.changePct > 0"
                            [class.text-success]="row.changePct > 0"
                            [class.bg-danger-soft]="row.changePct < 0"
                            [class.text-danger]="row.changePct < 0"
                            [class.bg-subtle]="row.changePct === 0"
                            [class.text-fg-2]="row.changePct === 0"
                          >
                            {{ formatDelta(row.changePct) }}
                          </span>
                        }
                      </td>
                      <td class="px-3 py-3 text-fg">{{ row.changeReason }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>
      }
    </div>

    <kadr-drawer
      [open]="drawerOpen()"
      [title]="'employees.salary.editStructure' | transloco"
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
  private readonly insuranceApi = inject(EmployeeInsuranceApi);
  private readonly otApi = inject(OvertimeMultipliersApi);
  private readonly payComponentsApi = inject(PayComponentsApi);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(TranslocoService);

  readonly employeeId = input.required<string>();

  protected readonly state = signal<PanelState>('loading');
  protected readonly current = signal<EmployeeSalary | null>(null);
  protected readonly history = signal<EmployeeSalaryListItem[]>([]);
  protected readonly insurance = signal<EmployeeInsuranceProfile | null>(null);
  protected readonly ot = signal<OvertimeMultipliers | null>(null);
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

  protected readonly gross = computed(() => {
    const cur = this.current();
    if (!cur) return 0;
    return cur.basicAmount + cur.components.reduce((s, c) => s + c.amountOrRate, 0);
  });

  protected readonly structureLines = computed((): StructureLine[] => {
    const cur = this.current();
    if (!cur) return [];
    const gross = this.gross();
    const raw = [
      {
        key: 'basic',
        nameAr: this.i18n.translate('employees.salary.basic'),
        nameEn: 'Basic',
        amount: cur.basicAmount,
        isTaxable: true,
        isInsurable: true,
      },
      ...cur.components.map((c) => ({
        key: c.id,
        nameAr: c.payComponentNameAr,
        nameEn: c.payComponentNameEn,
        amount: c.amountOrRate,
        isTaxable: c.isTaxable,
        isInsurable: c.isInsurable,
      })),
    ];
    return raw.map((r, i) => ({
      ...r,
      pct: gross > 0 ? (r.amount / gross) * 100 : 0,
      barClass: BAR_CLASSES[i % BAR_CLASSES.length]!,
    }));
  });

  protected readonly insuredWage = computed((): number | null => {
    const profile = this.insurance();
    if (profile?.isInsured && profile.insurableWageOverride != null) {
      return profile.insurableWageOverride;
    }
    const cur = this.current();
    if (!cur) return null;
    let sum = 0;
    // Basic is always insurable in seeded CreateBasic.
    sum += cur.basicAmount;
    for (const c of cur.components) {
      if (c.isInsurable) sum += c.amountOrRate;
    }
    return sum;
  });

  protected readonly grossDeltaPct = computed((): number | null => {
    const rows = this.history();
    if (rows.length < 2) return null;
    const currentGross = rows[0]!.grossAmount;
    const previousGross = rows[1]!.grossAmount;
    if (previousGross <= 0) return null;
    return ((currentGross - previousGross) / previousGross) * 100;
  });

  protected readonly historyRows = computed(() => {
    const rows = this.history();
    return rows.map((row, i) => {
      const older = rows[i + 1];
      let changePct: number | null = null;
      if (older && older.grossAmount > 0) {
        changePct = ((row.grossAmount - older.grossAmount) / older.grossAmount) * 100;
      }
      return {
        ...row,
        isCurrent: row.effectiveTo == null,
        changePct,
      };
    });
  });

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

  formatMultiplierPct(multiplier: number): string {
    return `${(multiplier * 100).toLocaleString(undefined, { maximumFractionDigits: 0 })}%`;
  }

  formatDelta(pct: number): string {
    const sign = pct > 0 ? '+' : '';
    return `${sign}${pct.toLocaleString(undefined, { maximumFractionDigits: 1 })}%`;
  }

  componentName(ar: string, en: string): string {
    return this.i18n.getActiveLang() === 'en' ? en || ar : ar;
  }

  classificationLabel(isTaxable: boolean, isInsurable: boolean): string {
    if (isTaxable && isInsurable) {
      return this.i18n.translate('employees.salary.classTaxIns');
    }
    if (isTaxable) {
      return this.i18n.translate('employees.salary.classTax');
    }
    if (isInsurable) {
      return this.i18n.translate('employees.salary.classIns');
    }
    return this.i18n.translate('employees.salary.classOther');
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
      const [current, page, insurance, ot] = await Promise.all([
        this.api.getCurrent(id),
        this.api.list(id, 1, 100),
        this.insuranceApi.get(id).catch(() => null),
        this.otApi.get().catch(() => null),
      ]);
      this.current.set(current);
      this.history.set(page.items);
      this.insurance.set(insurance);
      this.ot.set(ot);
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
