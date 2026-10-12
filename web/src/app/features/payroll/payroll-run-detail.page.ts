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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  PayrollEntryDetail,
  PayrollEntryListItem,
  PayrollRunDetail,
  PayrollRunsApi,
} from '../../core/api/payroll-runs.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-payroll-run-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    SkeletonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    DrawerComponent,
    ConfirmDialogComponent,
    IconComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-5">
      <nav class="text-sm text-fg-2" [attr.aria-label]="'payroll.runs.breadcrumbAria' | transloco">
        <a routerLink="/payroll/runs" class="text-accent hover:underline">
          {{ 'payroll.runs.title' | transloco }}
        </a>
        <span class="mx-1.5 text-fg-muted">/</span>
        <span class="text-fg">{{ 'payroll.runs.review' | transloco }}</span>
      </nav>

      @if (state() === 'loading') {
        <kadr-skeleton height="4rem" />
        <div class="grid grid-cols-2 gap-3 lg:grid-cols-5">
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
          <kadr-skeleton height="5rem" />
        </div>
        <kadr-skeleton height="16rem" />
      } @else if (state() === 'error' || !run()) {
        <kadr-network-error
          [title]="'payroll.runs.loadErrorTitle' | transloco"
          [description]="'payroll.runs.loadErrorBody' | transloco"
          (retry)="reload()"
        />
      } @else {
        @let r = run()!;
        <header class="flex flex-col gap-3 text-start lg:flex-row lg:items-start lg:justify-between">
          <div class="min-w-0">
            <div class="flex flex-wrap items-center gap-2">
              <h1 class="text-2xl font-bold leading-tight text-fg">
                {{
                  'payroll.runs.detailTitle'
                    | transloco: { period: periodLabel(r.periodEnd) }
                }}
              </h1>
              <span [class]="statusClass(r.status)">
                <kadr-icon [name]="statusIcon(r.status)" size="sm" class="me-1" />
                {{ statusLabel(r.status) }}
              </span>
            </div>
            <p class="mt-1 text-sm tabular-nums text-fg-2">
              {{ formatDate(r.periodStart) }} – {{ formatDate(r.periodEnd) }}
            </p>
          </div>
          <div class="flex flex-wrap gap-2">
            @if (canRun() && (r.status === 'Draft' || r.status === 'InReview')) {
              <kadr-button
                variant="secondary"
                type="button"
                [loading]="actionBusy() === 'calculate'"
                (click)="doCalculate()"
              >
                {{
                  (r.status === 'Draft'
                    ? 'payroll.runs.actionCalculate'
                    : 'payroll.runs.actionRecalculate'
                  ) | transloco
                }}
              </kadr-button>
            }
            @if (canRun() && r.status === 'Draft') {
              <kadr-button
                variant="primary"
                type="button"
                [loading]="actionBusy() === 'submit'"
                (click)="doSubmit()"
              >
                {{ 'payroll.runs.actionSubmit' | transloco }}
              </kadr-button>
            }
            @if (canApprove() && r.status === 'InReview') {
              <kadr-button
                variant="primary"
                type="button"
                [loading]="actionBusy() === 'approve'"
                (click)="doApprove()"
              >
                {{ 'payroll.runs.actionApprove' | transloco }}
              </kadr-button>
            }
            @if (canLock() && r.status === 'Approved') {
              <kadr-button
                variant="primary"
                type="button"
                [loading]="actionBusy() === 'lock'"
                (click)="doLock()"
              >
                {{ 'payroll.runs.actionLock' | transloco }}
              </kadr-button>
            }
            @if (canReopen() && r.status === 'Locked') {
              <kadr-button
                variant="danger-soft"
                type="button"
                (click)="reopenOpen.set(true)"
              >
                {{ 'payroll.runs.actionReopen' | transloco }}
              </kadr-button>
            }
          </div>
        </header>

        @if (r.warningCount > 0) {
          <div
            class="flex flex-col gap-2 rounded-lg border border-line bg-danger-soft px-4 py-3 text-start sm:flex-row sm:items-center sm:justify-between"
            role="status"
          >
            <div class="flex gap-3">
              <kadr-icon name="alert" size="sm" class="mt-0.5 shrink-0 text-danger" />
              <p class="text-sm text-fg">
                {{ 'payroll.runs.warningsBanner' | transloco: { n: r.warningCount } }}
              </p>
            </div>
            <a
              routerLink="/settings/payroll-rules"
              class="text-sm font-medium text-accent hover:underline"
            >
              {{ 'payroll.runs.reviewRules' | transloco }}
            </a>
          </div>
        }

        <div class="grid grid-cols-2 gap-3 lg:grid-cols-5">
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'payroll.runs.kpiGross' | transloco }}</p>
            <p class="mt-1 text-xl font-bold tabular-nums text-fg">
              {{ formatMoney(r.totalGross) }}
            </p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'payroll.runs.kpiNet' | transloco }}</p>
            <p class="mt-1 text-xl font-bold tabular-nums text-accent">
              {{ formatMoney(r.totalNet) }}
            </p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'payroll.runs.kpiEmployer' | transloco }}</p>
            <p class="mt-1 text-xl font-bold tabular-nums text-fg">
              {{ formatMoney(r.totalEmployerCost) }}
            </p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'payroll.runs.kpiHeadcount' | transloco }}</p>
            <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ r.headcount }}</p>
          </div>
          <div class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-xs text-fg-2">{{ 'payroll.runs.kpiAlerts' | transloco }}</p>
            <p class="mt-1 text-xl font-bold tabular-nums text-fg">{{ r.warningCount }}</p>
          </div>
        </div>

        <div class="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div class="min-w-0 flex-1">
            <kadr-input
              [label]="'payroll.runs.entrySearch' | transloco"
              [value]="entryQuery()"
              (valueChange)="onEntrySearch($event)"
            />
          </div>
          <kadr-button
            [variant]="warningsOnly() ? 'primary' : 'secondary'"
            type="button"
            (click)="toggleWarnings()"
          >
            {{
              'payroll.runs.warningsOnly'
                | transloco: { n: r.warningCount }
            }}
          </kadr-button>
        </div>

        @if (entriesState() === 'loading') {
          <kadr-skeleton height="12rem" />
        } @else if (entriesState() === 'error') {
          <kadr-network-error
            [title]="'payroll.runs.entriesErrorTitle' | transloco"
            [description]="'payroll.runs.entriesErrorBody' | transloco"
            (retry)="loadEntries()"
          />
        } @else if (entries().length === 0) {
          <kadr-empty-state
            [title]="'payroll.runs.entriesEmptyTitle' | transloco"
            [description]="'payroll.runs.entriesEmptyBody' | transloco"
          >
            @if (canRun() && r.status === 'Draft') {
              <kadr-button variant="primary" type="button" (click)="doCalculate()">
                {{ 'payroll.runs.actionCalculate' | transloco }}
              </kadr-button>
            }
          </kadr-empty-state>
        } @else {
          <div class="hidden overflow-x-auto rounded-xl border border-line bg-surface shadow-sm md:block">
            <table class="w-full text-start text-sm">
              <thead class="sticky top-0 bg-subtle text-xs text-fg-2">
                <tr>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colEmployee' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colGross' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colNet' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colEmployer' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colEntryStatus' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'payroll.runs.colActions' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                @for (entry of entries(); track entry.id) {
                  <tr class="border-t border-line hover:bg-subtle/60">
                    <td class="px-3 py-3">
                      <button
                        type="button"
                        class="text-start"
                        (click)="openEntry(entry)"
                      >
                        <div class="font-medium text-fg">{{ displayName(entry) }}</div>
                        <div class="mt-0.5 font-mono text-xs text-fg-muted">
                          {{ entry.employeeNumber }}
                        </div>
                      </button>
                    </td>
                    <td class="px-3 py-3 tabular-nums text-fg">{{ formatMoney(entry.gross) }}</td>
                    <td class="px-3 py-3 tabular-nums font-medium text-fg">
                      {{ formatMoney(entry.net) }}
                    </td>
                    <td class="px-3 py-3 tabular-nums text-fg-2">
                      {{ formatMoney(entry.employerCostTotal) }}
                    </td>
                    <td class="px-3 py-3">
                      @if (entry.isHeld) {
                        <span
                          class="inline-flex items-center rounded-full bg-danger-soft px-2 py-0.5 text-xs font-medium text-danger"
                        >
                          <kadr-icon name="alert" size="sm" class="me-1" />
                          {{ 'payroll.runs.entryHeld' | transloco }}
                        </span>
                      } @else if (entry.warningCount > 0) {
                        <span
                          class="inline-flex items-center rounded-full bg-warning-soft px-2 py-0.5 text-xs font-medium text-fg"
                        >
                          <kadr-icon name="alert" size="sm" class="me-1" />
                          {{
                            'payroll.runs.entryWarnings'
                              | transloco: { n: entry.warningCount }
                          }}
                        </span>
                      } @else {
                        <span
                          class="inline-flex items-center rounded-full bg-success-soft px-2 py-0.5 text-xs font-medium text-success"
                        >
                          <kadr-icon name="check" size="sm" class="me-1" />
                          {{ 'payroll.runs.entryOk' | transloco }}
                        </span>
                      }
                    </td>
                    <td class="px-3 py-3">
                      <kadr-button
                        variant="ghost"
                        type="button"
                        size="sm"
                        (click)="openEntry(entry)"
                      >
                        {{ 'payroll.runs.viewEntry' | transloco }}
                      </kadr-button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <ul class="flex flex-col gap-3 md:hidden">
            @for (entry of entries(); track entry.id) {
              <li class="rounded-xl border border-line bg-surface p-4 text-start shadow-sm">
                <button type="button" class="w-full text-start" (click)="openEntry(entry)">
                  <p class="font-medium text-fg">{{ displayName(entry) }}</p>
                  <p class="mt-0.5 font-mono text-xs text-fg-muted">{{ entry.employeeNumber }}</p>
                  <dl class="mt-3 grid grid-cols-2 gap-2 text-xs">
                    <div>
                      <dt class="text-fg-2">{{ 'payroll.runs.colGross' | transloco }}</dt>
                      <dd class="mt-0.5 tabular-nums font-medium text-fg">
                        {{ formatMoney(entry.gross) }}
                      </dd>
                    </div>
                    <div>
                      <dt class="text-fg-2">{{ 'payroll.runs.colNet' | transloco }}</dt>
                      <dd class="mt-0.5 tabular-nums font-medium text-fg">
                        {{ formatMoney(entry.net) }}
                      </dd>
                    </div>
                  </dl>
                </button>
              </li>
            }
          </ul>

          <div class="flex flex-wrap items-center justify-between gap-2 text-sm text-fg-2">
            <p>
              {{
                'payroll.runs.entriesShowing'
                  | transloco: { n: entries().length, total: entriesTotal() }
              }}
            </p>
            <div class="flex gap-2">
              <kadr-button
                variant="secondary"
                type="button"
                [disabled]="entryPage() <= 1"
                (click)="prevEntries()"
              >
                {{ 'payroll.runs.prev' | transloco }}
              </kadr-button>
              <kadr-button
                variant="secondary"
                type="button"
                [disabled]="entryPage() * entryPageSize >= entriesTotal()"
                (click)="nextEntries()"
              >
                {{ 'payroll.runs.next' | transloco }}
              </kadr-button>
            </div>
          </div>
        }
      }
    </div>

    <kadr-drawer
      [open]="!!entryDetail() || entryLoading()"
      [title]="'payroll.runs.entryTitle' | transloco"
      [subtitle]="
        entryDetail()
          ? displayNameFromDetail(entryDetail()!)
          : undefined
      "
      [closeLabel]="'ui.close' | transloco"
      size="lg"
      (closed)="closeEntry()"
    >
      @if (entryLoading()) {
        <kadr-skeleton height="16rem" />
      } @else if (entryError()) {
        <kadr-network-error
          [title]="'payroll.runs.entryErrorTitle' | transloco"
          [description]="'payroll.runs.entryErrorBody' | transloco"
          (retry)="selectedEntryId() && openEntryById(selectedEntryId()!)"
        />
      } @else if (entryDetail(); as d) {
        <div class="flex flex-col gap-4 text-start">
          <div class="rounded-lg bg-accent px-4 py-3 text-on-accent">
            <p class="text-xs opacity-90">{{ 'payroll.runs.entryNetDue' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums">{{ formatMoney(d.net) }}</p>
          </div>

          <dl class="grid grid-cols-2 gap-3 text-sm">
            <div>
              <dt class="text-fg-2">{{ 'payroll.runs.colGross' | transloco }}</dt>
              <dd class="mt-0.5 tabular-nums font-medium text-fg">{{ formatMoney(d.gross) }}</dd>
            </div>
            <div>
              <dt class="text-fg-2">{{ 'payroll.runs.entryInsuranceEmp' | transloco }}</dt>
              <dd class="mt-0.5 tabular-nums font-medium text-fg">
                {{ formatMoney(d.employeeInsurance) }}
              </dd>
            </div>
            <div>
              <dt class="text-fg-2">{{ 'payroll.runs.entryTax' | transloco }}</dt>
              <dd class="mt-0.5 tabular-nums font-medium text-fg">{{ formatMoney(d.incomeTax) }}</dd>
            </div>
            <div>
              <dt class="text-fg-2">{{ 'payroll.runs.entryOtherDed' | transloco }}</dt>
              <dd class="mt-0.5 tabular-nums font-medium text-fg">
                {{ formatMoney(d.otherDeductions) }}
              </dd>
            </div>
          </dl>

          @if (d.warningCount > 0 || d.isHeld) {
            <div class="rounded-lg border border-line bg-warning-soft px-3 py-2 text-sm text-fg">
              @if (d.isHeld) {
                <p class="font-medium">
                  {{ 'payroll.runs.entryHeld' | transloco }}
                  @if (d.holdReason) {
                    <span class="font-normal text-fg-2"> — {{ d.holdReason }}</span>
                  }
                </p>
              }
              @if (d.warningCount > 0) {
                <p class="mt-1">
                  {{ 'payroll.runs.entryWarnings' | transloco: { n: d.warningCount } }}
                </p>
              }
            </div>
          }

          <div>
            <h3 class="text-sm font-bold text-fg">{{ 'payroll.runs.entryLines' | transloco }}</h3>
            <ul class="mt-2 divide-y divide-line rounded-lg border border-line">
              @for (line of d.lines; track line.id) {
                <li class="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                  <span class="min-w-0 text-fg">{{ lineLabel(line.labelAr, line.labelEn) }}</span>
                  <span class="shrink-0 tabular-nums font-medium text-fg">
                    {{ formatMoney(line.amount) }}
                  </span>
                </li>
              } @empty {
                <li class="px-3 py-4 text-sm text-fg-muted">
                  {{ 'payroll.runs.entryLinesEmpty' | transloco }}
                </li>
              }
            </ul>
          </div>

          <div drawerFooter class="flex w-full flex-wrap gap-2">
            @if (canRun() && !d.isHeld && run()?.status !== 'Locked') {
              <kadr-button
                variant="danger-soft"
                type="button"
                (click)="holdOpen.set(true)"
              >
                {{ 'payroll.runs.actionHold' | transloco }}
              </kadr-button>
            }
            <kadr-button variant="secondary" type="button" (click)="closeEntry()">
              {{ 'ui.close' | transloco }}
            </kadr-button>
          </div>
        </div>
      }
    </kadr-drawer>

    <kadr-drawer
      [open]="reopenOpen()"
      [title]="'payroll.runs.reopenTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="reopenOpen.set(false)"
    >
      <form class="flex flex-col gap-4 text-start" (ngSubmit)="doReopen()">
        <p class="text-sm text-fg-2">{{ 'payroll.runs.reopenBody' | transloco }}</p>
        <label class="flex flex-col gap-2 text-sm text-fg">
          <span class="font-medium">{{ 'payroll.runs.reason' | transloco }}</span>
          <textarea
            class="min-h-20 rounded-md border border-line-strong bg-surface px-3 py-2 text-fg"
            [value]="reopenReason()"
            (input)="reopenReason.set($any($event.target).value)"
            required
          ></textarea>
        </label>
        <div drawerFooter class="flex w-full flex-wrap gap-2">
          <kadr-button variant="danger-soft" type="submit" [loading]="actionBusy() === 'reopen'">
            {{ 'payroll.runs.actionReopen' | transloco }}
          </kadr-button>
          <kadr-button variant="ghost" type="button" (click)="reopenOpen.set(false)">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
        </div>
      </form>
    </kadr-drawer>

    <kadr-confirm-dialog
      [open]="holdOpen()"
      [title]="'payroll.runs.holdTitle' | transloco"
      [message]="'payroll.runs.holdBody' | transloco"
      [confirmLabel]="'payroll.runs.actionHold' | transloco"
      [cancelLabel]="'ui.cancel' | transloco"
      [danger]="true"
      [busy]="actionBusy() === 'hold'"
      (confirmed)="doHold()"
      (cancelled)="holdOpen.set(false)"
    />
  `,
})
export class PayrollRunDetailPage implements OnInit {
  private readonly api = inject(PayrollRunsApi);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly title = inject(Title);
  private readonly route = inject(ActivatedRoute);

  protected readonly state = signal<PageState>('loading');
  protected readonly run = signal<PayrollRunDetail | null>(null);
  protected readonly entriesState = signal<PageState>('loading');
  protected readonly entries = signal<PayrollEntryListItem[]>([]);
  protected readonly entriesTotal = signal(0);
  protected readonly entryPage = signal(1);
  protected readonly entryPageSize = 50;
  protected readonly entryQuery = signal('');
  protected readonly warningsOnly = signal(false);
  protected readonly actionBusy = signal<string | null>(null);

  protected readonly selectedEntryId = signal<string | null>(null);
  protected readonly entryDetail = signal<PayrollEntryDetail | null>(null);
  protected readonly entryLoading = signal(false);
  protected readonly entryError = signal(false);

  protected readonly reopenOpen = signal(false);
  protected readonly reopenReason = signal('');
  protected readonly holdOpen = signal(false);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly canRun = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.PayrollRun) && !this.subscription.readOnly(),
  );
  protected readonly canApprove = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.PayrollApprove) && !this.subscription.readOnly(),
  );
  protected readonly canLock = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.PayrollLock) && !this.subscription.readOnly(),
  );
  protected readonly canReopen = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.PayrollReopen) && !this.subscription.readOnly(),
  );

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('payroll.runs.review'));
    void this.reload();
  }

  async reload(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.state.set('error');
      return;
    }
    this.state.set('loading');
    try {
      const run = await this.api.get(id);
      this.run.set(run);
      this.state.set('ready');
      this.title.setTitle(
        this.transloco.translate('payroll.runs.detailTitle', {
          period: this.periodLabel(run.periodEnd),
        }),
      );
      await this.loadEntries();
    } catch {
      this.state.set('error');
    }
  }

  async loadEntries(): Promise<void> {
    const run = this.run();
    if (!run) return;
    this.entriesState.set('loading');
    try {
      const page = await this.api.listEntries(run.id, {
        page: this.entryPage(),
        pageSize: this.entryPageSize,
        q: this.entryQuery() || undefined,
        warnings: this.warningsOnly() ? true : undefined,
      });
      this.entries.set(page.items);
      this.entriesTotal.set(page.totalCount);
      this.entriesState.set('ready');
    } catch {
      this.entriesState.set('error');
    }
  }

  onEntrySearch(value: string): void {
    this.entryQuery.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.entryPage.set(1);
      void this.loadEntries();
    }, 300);
  }

  toggleWarnings(): void {
    this.warningsOnly.update((v) => !v);
    this.entryPage.set(1);
    void this.loadEntries();
  }

  prevEntries(): void {
    if (this.entryPage() <= 1) return;
    this.entryPage.update((p) => p - 1);
    void this.loadEntries();
  }

  nextEntries(): void {
    if (this.entryPage() * this.entryPageSize >= this.entriesTotal()) return;
    this.entryPage.update((p) => p + 1);
    void this.loadEntries();
  }

  displayName(entry: PayrollEntryListItem): string {
    if (this.transloco.getActiveLang() === 'en' && entry.fullNameEn) {
      return entry.fullNameEn;
    }
    return entry.fullNameAr;
  }

  displayNameFromDetail(d: PayrollEntryDetail): string {
    if (this.transloco.getActiveLang() === 'en' && d.fullNameEn) {
      return d.fullNameEn;
    }
    return d.fullNameAr;
  }

  lineLabel(ar: string, en: string): string {
    return this.transloco.getActiveLang() === 'en' && en ? en : ar;
  }

  async openEntry(entry: PayrollEntryListItem): Promise<void> {
    await this.openEntryById(entry.id);
  }

  async openEntryById(id: string): Promise<void> {
    this.selectedEntryId.set(id);
    this.entryLoading.set(true);
    this.entryError.set(false);
    this.entryDetail.set(null);
    try {
      const detail = await this.api.getEntry(id);
      this.entryDetail.set(detail);
    } catch {
      this.entryError.set(true);
    } finally {
      this.entryLoading.set(false);
    }
  }

  closeEntry(): void {
    this.selectedEntryId.set(null);
    this.entryDetail.set(null);
    this.entryError.set(false);
    this.holdOpen.set(false);
  }

  private async afterAction(run: PayrollRunDetail, toastKey: string): Promise<void> {
    this.run.set(run);
    this.toast.show(this.transloco.translate(toastKey), 'success');
    await this.loadEntries();
  }

  private failToast(err: unknown): void {
    const code = problemDetailsFromError(err)?.code;
    const key =
      code === 'payroll.rules.not_confirmed'
        ? 'payroll.runs.toastRulesNotConfirmed'
        : code === 'payroll.run.second_approver_required'
          ? 'payroll.runs.toastSecondApprover'
          : 'payroll.runs.toastFail';
    this.toast.show(this.transloco.translate(key), 'danger');
  }

  async doCalculate(): Promise<void> {
    const run = this.run();
    if (!run) return;
    this.actionBusy.set('calculate');
    try {
      const updated = await this.api.calculate(run.id);
      await this.afterAction(updated, 'payroll.runs.toastCalculated');
    } catch (err) {
      this.failToast(err);
    } finally {
      this.actionBusy.set(null);
    }
  }

  async doSubmit(): Promise<void> {
    const run = this.run();
    if (!run) return;
    this.actionBusy.set('submit');
    try {
      const updated = await this.api.submit(run.id);
      await this.afterAction(updated, 'payroll.runs.toastSubmitted');
    } catch (err) {
      this.failToast(err);
    } finally {
      this.actionBusy.set(null);
    }
  }

  async doApprove(): Promise<void> {
    const run = this.run();
    if (!run) return;
    this.actionBusy.set('approve');
    try {
      const updated = await this.api.approve(run.id);
      await this.afterAction(updated, 'payroll.runs.toastApproved');
    } catch (err) {
      this.failToast(err);
    } finally {
      this.actionBusy.set(null);
    }
  }

  async doLock(): Promise<void> {
    const run = this.run();
    if (!run) return;
    this.actionBusy.set('lock');
    try {
      const updated = await this.api.lock(run.id);
      await this.afterAction(updated, 'payroll.runs.toastLocked');
    } catch (err) {
      this.failToast(err);
    } finally {
      this.actionBusy.set(null);
    }
  }

  async doReopen(): Promise<void> {
    const run = this.run();
    const reason = this.reopenReason().trim();
    if (!run || !reason) {
      this.toast.show(this.transloco.translate('payroll.runs.toastValidation'), 'danger');
      return;
    }
    this.actionBusy.set('reopen');
    try {
      const updated = await this.api.reopen(run.id, { reason });
      this.reopenOpen.set(false);
      this.reopenReason.set('');
      await this.afterAction(updated, 'payroll.runs.toastReopened');
    } catch (err) {
      this.failToast(err);
    } finally {
      this.actionBusy.set(null);
    }
  }

  async doHold(): Promise<void> {
    const detail = this.entryDetail();
    if (!detail) return;
    this.actionBusy.set('hold');
    try {
      const updated = await this.api.holdEntry(detail.id, {});
      this.entryDetail.set(updated);
      this.holdOpen.set(false);
      this.entries.update((list) =>
        list.map((e) =>
          e.id === updated.id
            ? {
                ...e,
                isHeld: updated.isHeld,
                holdReason: updated.holdReason,
              }
            : e,
        ),
      );
      this.toast.show(this.transloco.translate('payroll.runs.toastHeld'), 'success');
    } catch (err) {
      this.failToast(err);
    } finally {
      this.actionBusy.set(null);
    }
  }

  periodLabel(periodEnd: string): string {
    const d = new Date(`${periodEnd}T00:00:00Z`);
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-GB';
    return new Intl.DateTimeFormat(lang, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(
      d,
    );
  }

  statusLabel(status: string): string {
    const key = `payroll.runs.status.${status}`;
    const t = this.transloco.translate(key);
    return t === key ? status : t;
  }

  statusIcon(status: string): 'clock' | 'check' | 'lock' | 'alert' | 'file' | 'refresh' {
    if (status === 'Locked') return 'lock';
    if (status === 'Approved') return 'check';
    if (status === 'InReview' || status === 'Calculating') return 'clock';
    if (status === 'Superseded') return 'refresh';
    if (status === 'Draft') return 'file';
    return 'alert';
  }

  statusClass(status: string): string {
    const base =
      'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ';
    if (status === 'Locked') return base + 'bg-success-soft text-success';
    if (status === 'Approved') return base + 'bg-accent-soft text-accent-soft-fg';
    if (status === 'InReview' || status === 'Calculating') {
      return base + 'bg-warning-soft text-fg';
    }
    if (status === 'Superseded') return base + 'bg-danger-soft text-danger';
    return base + 'bg-subtle text-fg-2';
  }

  formatMoney(value: number | null | undefined): string {
    if (value == null) return '—';
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-EG';
    return new Intl.NumberFormat(lang, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }

  formatDate(iso: string): string {
    const d = new Date(`${iso}T00:00:00Z`);
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-GB';
    return new Intl.DateTimeFormat(lang, { dateStyle: 'medium', timeZone: 'UTC' }).format(d);
  }
}
