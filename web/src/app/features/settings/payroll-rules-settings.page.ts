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
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  PayrollRuleHistoryItem,
  PayrollRuleListItem,
  PayrollRulesApi,
} from '../../core/api/payroll-rules.api';
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
type RulesTab = 'general' | 'legal';

interface TaxBracketRow {
  from: number;
  to: number | null;
  rate: number;
}

const GENERAL_KEYS = new Set([
  'payroll.period_start_day',
  'payroll.working_days_basis',
  'payroll.second_approval_required',
  'payroll.rounding',
  'payroll.tax_method',
]);

@Component({
  selector: 'kadr-payroll-rules-settings-page',
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
    ConfirmDialogComponent,
    IconComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-5">
      <header class="flex flex-col gap-3 text-start sm:flex-row sm:items-end sm:justify-between">
        <div class="flex min-w-0 flex-col gap-2">
          <h1 class="text-2xl font-bold leading-tight text-fg">
            {{ 'settings.payrollRules.title' | transloco }}
          </h1>
          <p class="text-sm text-fg-2">{{ 'settings.payrollRules.subtitle' | transloco }}</p>
        </div>
        @if (canMutate() && state() === 'ready' && !confirmedForLock()) {
          <kadr-button variant="primary" type="button" (click)="confirmOpen.set(true)">
            {{ 'settings.payrollRules.confirmCta' | transloco }}
          </kadr-button>
        }
      </header>

      @if (state() === 'loading') {
        <kadr-skeleton height="4rem" />
        <kadr-skeleton height="3rem" />
        <div class="grid gap-3 md:grid-cols-2">
          <kadr-skeleton height="8rem" />
          <kadr-skeleton height="8rem" />
          <kadr-skeleton height="8rem" />
          <kadr-skeleton height="8rem" />
        </div>
      } @else if (state() === 'error') {
        <kadr-network-error
          [title]="'settings.payrollRules.loadErrorTitle' | transloco"
          [description]="'settings.payrollRules.loadErrorBody' | transloco"
          (retry)="reload()"
        />
      } @else {
        <div
          class="flex gap-3 rounded-lg border border-line bg-subtle px-4 py-3 text-start"
          role="note"
        >
          <kadr-icon name="shield" size="sm" class="mt-0.5 shrink-0 text-fg-2" />
          <p class="text-sm leading-relaxed text-fg-2">
            {{ 'settings.payrollRules.disclaimer' | transloco }}
          </p>
        </div>

        @if (confirmedForLock()) {
          <div
            class="flex gap-3 rounded-lg border border-line bg-success-soft px-4 py-3 text-start"
            role="status"
          >
            <kadr-icon name="check" size="sm" class="mt-0.5 shrink-0 text-success" />
            <div class="min-w-0 text-sm text-fg">
              <p class="font-medium">{{ 'settings.payrollRules.confirmedTitle' | transloco }}</p>
              <p class="mt-0.5 text-fg-2">
                {{
                  'settings.payrollRules.confirmedBody'
                    | transloco
                      : {
                          at: formatDateTime(confirmedAt()),
                          reason: confirmedReason() || '—',
                        }
                }}
              </p>
            </div>
          </div>
        }

        @if (updateCount() > 0) {
          <div
            class="flex flex-col gap-2 rounded-lg border border-line bg-warning-soft px-4 py-3 text-start sm:flex-row sm:items-center sm:justify-between"
            role="status"
          >
            <div class="flex gap-3">
              <kadr-icon name="info" size="sm" class="mt-0.5 shrink-0 text-fg" />
              <p class="text-sm text-fg">
                {{ 'settings.payrollRules.updateAvailable' | transloco: { n: updateCount() } }}
              </p>
            </div>
          </div>
        }

        <div
          class="flex flex-wrap gap-1 rounded-lg bg-subtle p-1"
          role="tablist"
          [attr.aria-label]="'settings.payrollRules.tabsAria' | transloco"
        >
          @for (tab of tabs; track tab.id) {
            <button
              type="button"
              role="tab"
              class="rounded-md px-3 py-1.5 text-sm transition-colors"
              [class.bg-surface]="activeTab() === tab.id"
              [class.text-fg]="activeTab() === tab.id"
              [class.font-medium]="activeTab() === tab.id"
              [class.shadow-sm]="activeTab() === tab.id"
              [class.text-fg-2]="activeTab() !== tab.id"
              [attr.aria-selected]="activeTab() === tab.id"
              (click)="activeTab.set(tab.id)"
            >
              {{ tab.labelKey | transloco }}
            </button>
          }
        </div>

        @if (visibleItems().length === 0) {
          <kadr-empty-state
            [title]="'settings.payrollRules.emptyTitle' | transloco"
            [description]="'settings.payrollRules.emptyBody' | transloco"
          />
        } @else {
          <div class="grid gap-3 md:grid-cols-2">
            @for (item of visibleItems(); track item.ruleKey) {
              <article
                class="flex flex-col gap-3 rounded-xl border border-line bg-surface p-4 text-start shadow-sm"
              >
                <div class="flex items-start justify-between gap-2">
                  <div class="min-w-0">
                    <h2 class="text-sm font-bold text-fg">
                      {{ ruleTitle(item.ruleKey) }}
                    </h2>
                    <p class="mt-0.5 font-mono text-xs text-fg-muted">{{ item.ruleKey }}</p>
                  </div>
                  <div class="flex shrink-0 flex-wrap justify-end gap-1">
                    @if (item.differsFromOfficialDefault || item.isCustomized) {
                      <span
                        class="inline-flex items-center rounded-full bg-accent-soft px-2 py-0.5 text-xs font-medium text-accent-soft-fg"
                      >
                        {{ 'settings.payrollRules.badgeCustomized' | transloco }}
                      </span>
                    } @else {
                      <span
                        class="inline-flex items-center rounded-full bg-success-soft px-2 py-0.5 text-xs font-medium text-success"
                      >
                        {{ 'settings.payrollRules.badgeDefault' | transloco }}
                      </span>
                    }
                    @if (item.updateAvailable) {
                      <span
                        class="inline-flex items-center rounded-full bg-warning-soft px-2 py-0.5 text-xs font-medium text-fg"
                      >
                        {{ 'settings.payrollRules.badgeUpdate' | transloco }}
                      </span>
                    }
                  </div>
                </div>

                <div class="text-2xl font-bold tabular-nums text-fg">
                  {{ formatValue(item) }}
                </div>

                @if (item.differsFromOfficialDefault && item.officialDefaultJson) {
                  <p class="text-xs text-fg-2">
                    {{ 'settings.payrollRules.officialDefault' | transloco }}:
                    <span class="font-medium tabular-nums text-fg">{{
                      formatJsonValue(item.officialDefaultJson, item.valueType, item.ruleKey)
                    }}</span>
                  </p>
                }

                <p class="text-xs text-fg-muted">
                  {{ 'settings.payrollRules.effectiveFrom' | transloco }}:
                  <span class="tabular-nums">{{ formatDate(item.effectiveFrom) }}</span>
                </p>

                @if (item.ruleKey === 'tax.brackets') {
                  <div class="overflow-x-auto rounded-lg border border-line">
                    <table class="w-full text-start text-xs">
                      <thead class="bg-subtle text-fg-2">
                        <tr>
                          <th class="px-2 py-1.5 font-medium">
                            {{ 'settings.payrollRules.bracketFrom' | transloco }}
                          </th>
                          <th class="px-2 py-1.5 font-medium">
                            {{ 'settings.payrollRules.bracketTo' | transloco }}
                          </th>
                          <th class="px-2 py-1.5 font-medium">
                            {{ 'settings.payrollRules.bracketRate' | transloco }}
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (b of parseBrackets(item.valueJson); track $index) {
                          <tr class="border-t border-line">
                            <td class="px-2 py-1.5 tabular-nums">{{ formatNumber(b.from) }}</td>
                            <td class="px-2 py-1.5 tabular-nums">
                              {{ b.to === null ? '∞' : formatNumber(b.to) }}
                            </td>
                            <td class="px-2 py-1.5 tabular-nums">{{ formatPercent(b.rate) }}</td>
                          </tr>
                        } @empty {
                          <tr>
                            <td colspan="3" class="px-2 py-3 text-fg-muted">
                              {{ 'settings.payrollRules.bracketsEmpty' | transloco }}
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }

                <div class="mt-auto flex flex-wrap gap-2 border-t border-line pt-3">
                  <kadr-button
                    variant="ghost"
                    type="button"
                    (click)="openHistory(item)"
                  >
                    {{ 'settings.payrollRules.history' | transloco }}
                  </kadr-button>
                  @if (canMutate()) {
                    <kadr-button variant="secondary" type="button" (click)="openEdit(item)">
                      {{ 'settings.payrollRules.edit' | transloco }}
                    </kadr-button>
                    @if (item.differsFromOfficialDefault || item.isCustomized) {
                      <kadr-button
                        variant="ghost"
                        type="button"
                        (click)="askReset(item)"
                      >
                        {{ 'settings.payrollRules.reset' | transloco }}
                      </kadr-button>
                    }
                    @if (item.updateAvailable && item.officialDefaultJson) {
                      <kadr-button
                        variant="secondary"
                        type="button"
                        (click)="applyOfficial(item)"
                      >
                        {{ 'settings.payrollRules.applyUpdate' | transloco }}
                      </kadr-button>
                    }
                  }
                </div>
              </article>
            }
          </div>
        }
      }
    </div>

    <kadr-drawer
      [open]="editOpen()"
      [title]="'settings.payrollRules.editTitle' | transloco"
      [subtitle]="editKey() ? ruleTitle(editKey()) : undefined"
      [closeLabel]="'ui.close' | transloco"
      size="lg"
      (closed)="closeEdit()"
    >
      <form class="flex flex-col gap-4 text-start" (ngSubmit)="saveEdit()">
        @if (editValueType() === 'money' || editValueType() === 'percent' || editValueType() === 'decimal' || editValueType() === 'int') {
          <kadr-input
            [label]="editFieldLabel()"
            [value]="editScalar()"
            (valueChange)="editScalar.set($event)"
            type="text"
          />
        } @else if (editValueType() === 'bool') {
          <label class="flex items-center gap-2 text-sm text-fg">
            <input
              type="checkbox"
              class="size-4 rounded border-line"
              [checked]="editBool()"
              (change)="editBool.set($any($event.target).checked)"
            />
            {{ ruleTitle(editKey()) }}
          </label>
        } @else if (editValueType() === 'enum') {
          <label class="flex flex-col gap-2 text-sm text-fg">
            <span class="font-medium">{{ 'settings.payrollRules.value' | transloco }}</span>
            <select
              class="h-control rounded-md border border-line-strong bg-surface px-3 text-fg"
              [value]="editEnum()"
              (change)="editEnum.set($any($event.target).value)"
            >
              @for (opt of enumOptions(editKey()); track opt) {
                <option [value]="opt">{{ enumLabel(editKey(), opt) }}</option>
              }
            </select>
          </label>
        } @else if (editValueType() === 'table') {
          <div class="flex flex-col gap-2">
            <div class="flex items-center justify-between gap-2">
              <p class="text-sm font-medium text-fg">
                {{ 'settings.payrollRules.bracketsTitle' | transloco }}
              </p>
              <kadr-button variant="secondary" type="button" (click)="addBracket()">
                {{ 'settings.payrollRules.addBracket' | transloco }}
              </kadr-button>
            </div>
            @for (b of editBrackets(); track $index; let i = $index) {
              <div class="grid grid-cols-3 gap-2">
                <label class="flex flex-col gap-1 text-xs text-fg-2">
                  {{ 'settings.payrollRules.bracketFrom' | transloco }}
                  <input
                    class="h-control rounded-md border border-line-strong bg-surface px-2 text-sm tabular-nums text-fg"
                    type="number"
                    [value]="b.from"
                    (change)="patchBracket(i, 'from', $any($event.target).value)"
                  />
                </label>
                <label class="flex flex-col gap-1 text-xs text-fg-2">
                  {{ 'settings.payrollRules.bracketTo' | transloco }}
                  <input
                    class="h-control rounded-md border border-line-strong bg-surface px-2 text-sm tabular-nums text-fg"
                    type="number"
                    [value]="b.to ?? ''"
                    [placeholder]="'∞'"
                    (change)="patchBracket(i, 'to', $any($event.target).value)"
                  />
                </label>
                <label class="flex flex-col gap-1 text-xs text-fg-2">
                  {{ 'settings.payrollRules.bracketRate' | transloco }}
                  <input
                    class="h-control rounded-md border border-line-strong bg-surface px-2 text-sm tabular-nums text-fg"
                    type="number"
                    [value]="b.rate"
                    (change)="patchBracket(i, 'rate', $any($event.target).value)"
                  />
                </label>
              </div>
            }
          </div>
        }

        <kadr-input
          [label]="'settings.payrollRules.effectiveFrom' | transloco"
          [value]="editEffectiveFrom()"
          (valueChange)="editEffectiveFrom.set($event)"
          type="date"
        />
        <label class="flex flex-col gap-2 text-sm text-fg">
          <span class="font-medium">{{ 'settings.payrollRules.reason' | transloco }}</span>
          <textarea
            class="min-h-20 rounded-md border border-line-strong bg-surface px-3 py-2 text-fg"
            [value]="editReason()"
            (input)="editReason.set($any($event.target).value)"
            required
          ></textarea>
        </label>
        <div drawerFooter class="flex w-full flex-wrap gap-2">
          <kadr-button variant="primary" type="submit" [loading]="busy()">
            {{ 'ui.save' | transloco }}
          </kadr-button>
          <kadr-button variant="ghost" type="button" (click)="closeEdit()">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
        </div>
      </form>
    </kadr-drawer>

    <kadr-drawer
      [open]="!!historyTarget()"
      [title]="'settings.payrollRules.historyTitle' | transloco"
      [subtitle]="historyTarget() ? ruleTitle(historyTarget()!.ruleKey) : undefined"
      [closeLabel]="'ui.close' | transloco"
      size="lg"
      [hasFooter]="false"
      (closed)="historyTarget.set(null)"
    >
      @if (historyLoading()) {
        <kadr-skeleton height="12rem" />
      } @else if (historyError()) {
        <kadr-network-error
          [title]="'settings.payrollRules.historyErrorTitle' | transloco"
          [description]="'settings.payrollRules.historyErrorBody' | transloco"
          (retry)="historyTarget() && openHistory(historyTarget()!)"
        />
      } @else if (historyItems().length === 0) {
        <kadr-empty-state
          [title]="'settings.payrollRules.historyEmptyTitle' | transloco"
          [description]="'settings.payrollRules.historyEmptyBody' | transloco"
        />
      } @else {
        <ul class="flex flex-col gap-3 text-start">
          @for (h of historyItems(); track h.id) {
            <li class="rounded-lg border border-line bg-surface p-3">
              <div class="flex flex-wrap items-center gap-2">
                <span class="text-sm font-medium tabular-nums text-fg">{{
                  formatJsonValue(h.valueJson, historyTarget()!.valueType, historyTarget()!.ruleKey)
                }}</span>
                @if (h.isCustomized) {
                  <span
                    class="rounded-full bg-accent-soft px-2 py-0.5 text-xs text-accent-soft-fg"
                  >
                    {{ 'settings.payrollRules.badgeCustomized' | transloco }}
                  </span>
                }
              </div>
              <p class="mt-1 text-xs text-fg-2">
                {{ 'settings.payrollRules.effectiveFrom' | transloco }}:
                <span class="tabular-nums">{{ formatDate(h.effectiveFrom) }}</span>
                ·
                <span class="tabular-nums">{{ formatDateTime(h.createdAt) }}</span>
              </p>
              <p class="mt-1 text-xs text-fg-muted">{{ h.reason }}</p>
            </li>
          }
        </ul>
      }
    </kadr-drawer>

    <kadr-drawer
      [open]="confirmOpen()"
      [title]="'settings.payrollRules.confirmTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="confirmOpen.set(false)"
    >
      <form class="flex flex-col gap-4 text-start" (ngSubmit)="confirmRules()">
        <p class="text-sm text-fg-2">{{ 'settings.payrollRules.confirmBody' | transloco }}</p>
        <label class="flex flex-col gap-2 text-sm text-fg">
          <span class="font-medium">{{ 'settings.payrollRules.reason' | transloco }}</span>
          <textarea
            class="min-h-20 rounded-md border border-line-strong bg-surface px-3 py-2 text-fg"
            [value]="confirmReason()"
            (input)="confirmReason.set($any($event.target).value)"
            required
          ></textarea>
        </label>
        <div drawerFooter class="flex w-full flex-wrap gap-2">
          <kadr-button variant="primary" type="submit" [loading]="busy()">
            {{ 'settings.payrollRules.confirmCta' | transloco }}
          </kadr-button>
          <kadr-button variant="ghost" type="button" (click)="confirmOpen.set(false)">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
        </div>
      </form>
    </kadr-drawer>

    <kadr-confirm-dialog
      [open]="!!resetTarget()"
      [title]="'settings.payrollRules.resetTitle' | transloco"
      [message]="'settings.payrollRules.resetBody' | transloco"
      [confirmLabel]="'settings.payrollRules.reset' | transloco"
      [cancelLabel]="'ui.cancel' | transloco"
      [danger]="true"
      [busy]="busy()"
      (confirmed)="confirmReset()"
      (cancelled)="resetTarget.set(null)"
    />
  `,
})
export class PayrollRulesSettingsPage implements OnInit {
  private readonly api = inject(PayrollRulesApi);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly title = inject(Title);

  protected readonly tabs: { id: RulesTab; labelKey: string }[] = [
    { id: 'general', labelKey: 'settings.payrollRules.tabGeneral' },
    { id: 'legal', labelKey: 'settings.payrollRules.tabLegal' },
  ];

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<PayrollRuleListItem[]>([]);
  protected readonly confirmedForLock = signal(false);
  protected readonly confirmedAt = signal<string | null>(null);
  protected readonly confirmedReason = signal<string | null>(null);
  protected readonly activeTab = signal<RulesTab>('legal');
  protected readonly busy = signal(false);

  protected readonly editOpen = signal(false);
  protected readonly editKey = signal('');
  protected readonly editValueType = signal('money');
  protected readonly editScalar = signal('');
  protected readonly editBool = signal(false);
  protected readonly editEnum = signal('');
  protected readonly editBrackets = signal<TaxBracketRow[]>([]);
  protected readonly editEffectiveFrom = signal('');
  protected readonly editReason = signal('');

  protected readonly historyTarget = signal<PayrollRuleListItem | null>(null);
  protected readonly historyItems = signal<PayrollRuleHistoryItem[]>([]);
  protected readonly historyLoading = signal(false);
  protected readonly historyError = signal(false);

  protected readonly confirmOpen = signal(false);
  protected readonly confirmReason = signal('');
  protected readonly resetTarget = signal<PayrollRuleListItem | null>(null);

  protected readonly canMutate = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.PayrollRulesManage) &&
      !this.subscription.readOnly(),
  );

  protected readonly updateCount = computed(
    () => this.items().filter((x) => x.updateAvailable).length,
  );

  protected readonly visibleItems = computed(() => {
    const tab = this.activeTab();
    return this.items().filter((x) =>
      tab === 'general' ? GENERAL_KEYS.has(x.ruleKey) : !GENERAL_KEYS.has(x.ruleKey),
    );
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('settings.payrollRules.title'));
    void this.reload();
  }

  async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const data = await this.api.list();
      this.items.set(data.items);
      this.confirmedForLock.set(data.rulesConfirmedForLock);
      this.confirmedAt.set(data.rulesConfirmedAt);
      this.confirmedReason.set(data.rulesConfirmedReason);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  ruleTitle(key: string): string {
    const i18nKey = `settings.payrollRules.keys.${key}`;
    const t = this.transloco.translate(i18nKey);
    return t === i18nKey ? key : t;
  }

  formatValue(item: PayrollRuleListItem): string {
    return this.formatJsonValue(item.valueJson, item.valueType, item.ruleKey);
  }

  formatJsonValue(json: string, valueType: string, ruleKey: string): string {
    try {
      const obj = JSON.parse(json) as Record<string, unknown>;
      if (valueType === 'money' && typeof obj['amount'] === 'number') {
        return `${this.formatNumber(obj['amount'] as number)} ${this.transloco.translate('settings.payrollRules.currency')}`;
      }
      if (valueType === 'percent' && typeof obj['rate'] === 'number') {
        return this.formatPercent(obj['rate'] as number);
      }
      if (valueType === 'decimal') {
        const m = obj['multiplier'];
        if (m == null) {
          return this.transloco.translate('settings.payrollRules.notSet');
        }
        return this.formatPercent((m as number) * 100);
      }
      if (valueType === 'int' && typeof obj['value'] === 'number') {
        return String(obj['value']);
      }
      if (valueType === 'bool') {
        return this.transloco.translate(
          obj['value'] === true
            ? 'settings.payrollRules.boolTrue'
            : 'settings.payrollRules.boolFalse',
        );
      }
      if (valueType === 'enum' && typeof obj['value'] === 'string') {
        return this.enumLabel(ruleKey, obj['value'] as string);
      }
      if (valueType === 'table') {
        const brackets = this.parseBrackets(json);
        return this.transloco.translate('settings.payrollRules.bracketsCount', {
          n: brackets.length,
        });
      }
      return json;
    } catch {
      return json;
    }
  }

  parseBrackets(json: string): TaxBracketRow[] {
    try {
      const obj = JSON.parse(json) as { brackets?: TaxBracketRow[] };
      return Array.isArray(obj.brackets) ? obj.brackets : [];
    } catch {
      return [];
    }
  }

  formatNumber(n: number): string {
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-EG';
    return new Intl.NumberFormat(lang, { maximumFractionDigits: 2 }).format(n);
  }

  formatPercent(n: number): string {
    return `${this.formatNumber(n)}%`;
  }

  formatDate(iso: string): string {
    if (!iso) return '—';
    const d = new Date(iso.length <= 10 ? `${iso}T00:00:00Z` : iso);
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-GB';
    return new Intl.DateTimeFormat(lang, { dateStyle: 'medium' }).format(d);
  }

  formatDateTime(iso: string | null): string {
    if (!iso) return '—';
    const d = new Date(iso);
    const lang = this.transloco.getActiveLang() === 'ar' ? 'ar-EG' : 'en-GB';
    return new Intl.DateTimeFormat(lang, { dateStyle: 'medium', timeStyle: 'short' }).format(d);
  }

  enumOptions(key: string): string[] {
    if (key === 'payroll.working_days_basis') {
      return ['calendar', 'fixed_30', 'working_days'];
    }
    if (key === 'payroll.rounding') {
      return ['half_away_from_zero'];
    }
    if (key === 'payroll.tax_method') {
      return ['annual_projection', 'monthly_slab'];
    }
    return [];
  }

  enumLabel(key: string, value: string): string {
    const i18nKey = `settings.payrollRules.enums.${key}.${value}`;
    const t = this.transloco.translate(i18nKey);
    return t === i18nKey ? value : t;
  }

  editFieldLabel(): string {
    const t = this.editValueType();
    if (t === 'percent') return this.transloco.translate('settings.payrollRules.rate');
    if (t === 'decimal') return this.transloco.translate('settings.payrollRules.multiplier');
    if (t === 'int') return this.transloco.translate('settings.payrollRules.value');
    return this.transloco.translate('settings.payrollRules.amount');
  }

  openEdit(item: PayrollRuleListItem): void {
    this.editKey.set(item.ruleKey);
    this.editValueType.set(item.valueType);
    this.editEffectiveFrom.set(item.effectiveFrom.slice(0, 10));
    this.editReason.set('');
    try {
      const obj = JSON.parse(item.valueJson) as Record<string, unknown>;
      if (item.valueType === 'money') {
        this.editScalar.set(String(obj['amount'] ?? ''));
      } else if (item.valueType === 'percent') {
        this.editScalar.set(String(obj['rate'] ?? ''));
      } else if (item.valueType === 'decimal') {
        this.editScalar.set(obj['multiplier'] == null ? '' : String(obj['multiplier']));
      } else if (item.valueType === 'int') {
        this.editScalar.set(String(obj['value'] ?? ''));
      } else if (item.valueType === 'bool') {
        this.editBool.set(obj['value'] === true);
      } else if (item.valueType === 'enum') {
        this.editEnum.set(String(obj['value'] ?? ''));
      } else if (item.valueType === 'table') {
        this.editBrackets.set(this.parseBrackets(item.valueJson));
      }
    } catch {
      this.editScalar.set('');
    }
    this.editOpen.set(true);
  }

  closeEdit(): void {
    this.editOpen.set(false);
  }

  addBracket(): void {
    const list = this.editBrackets();
    const last = list[list.length - 1];
    const from = last?.to ?? 0;
    this.editBrackets.set([...list, { from, to: null, rate: 0 }]);
  }

  patchBracket(index: number, field: 'from' | 'to' | 'rate', raw: string): void {
    this.editBrackets.update((list) =>
      list.map((b, i) => {
        if (i !== index) return b;
        if (field === 'to') {
          const trimmed = raw.trim();
          return { ...b, to: trimmed === '' ? null : Number(trimmed) };
        }
        return { ...b, [field]: Number(raw) };
      }),
    );
  }

  private buildValueJson(): string | null {
    const t = this.editValueType();
    if (t === 'money') {
      const amount = Number(this.editScalar());
      if (Number.isNaN(amount)) return null;
      return JSON.stringify({ amount });
    }
    if (t === 'percent') {
      const rate = Number(this.editScalar());
      if (Number.isNaN(rate)) return null;
      return JSON.stringify({ rate });
    }
    if (t === 'decimal') {
      const raw = this.editScalar().trim();
      if (raw === '') return JSON.stringify({ multiplier: null });
      const multiplier = Number(raw);
      if (Number.isNaN(multiplier)) return null;
      return JSON.stringify({ multiplier });
    }
    if (t === 'int') {
      const value = Number(this.editScalar());
      if (!Number.isInteger(value)) return null;
      return JSON.stringify({ value });
    }
    if (t === 'bool') {
      return JSON.stringify({ value: this.editBool() });
    }
    if (t === 'enum') {
      return JSON.stringify({ value: this.editEnum() });
    }
    if (t === 'table') {
      return JSON.stringify({ brackets: this.editBrackets() });
    }
    return null;
  }

  async saveEdit(): Promise<void> {
    const reason = this.editReason().trim();
    const effectiveFrom = this.editEffectiveFrom().trim();
    const valueJson = this.buildValueJson();
    if (!reason || !effectiveFrom || !valueJson) {
      this.toast.show(this.transloco.translate('settings.payrollRules.toastValidation'), 'danger');
      return;
    }
    this.busy.set(true);
    try {
      const updated = await this.api.upsert(this.editKey(), {
        valueJson,
        effectiveFrom,
        reason,
      });
      this.items.update((list) =>
        list.map((x) => (x.ruleKey === updated.ruleKey ? updated : x)),
      );
      this.confirmedForLock.set(false);
      this.closeEdit();
      this.toast.show(this.transloco.translate('settings.payrollRules.toastSaved'), 'success');
    } catch (err) {
      const code = problemDetailsFromError(err)?.code;
      this.toast.show(
        this.transloco.translate(
          code ? 'settings.payrollRules.toastFail' : 'settings.payrollRules.toastFail',
        ),
        'danger',
      );
    } finally {
      this.busy.set(false);
    }
  }

  async openHistory(item: PayrollRuleListItem): Promise<void> {
    this.historyTarget.set(item);
    this.historyLoading.set(true);
    this.historyError.set(false);
    this.historyItems.set([]);
    try {
      const rows = await this.api.history(item.ruleKey);
      this.historyItems.set(rows);
    } catch {
      this.historyError.set(true);
    } finally {
      this.historyLoading.set(false);
    }
  }

  askReset(item: PayrollRuleListItem): void {
    this.resetTarget.set(item);
  }

  async confirmReset(): Promise<void> {
    const item = this.resetTarget();
    if (!item) return;
    this.busy.set(true);
    try {
      const today = new Date().toISOString().slice(0, 10);
      const updated = await this.api.reset(item.ruleKey, {
        valueJson: item.officialDefaultJson ?? item.valueJson,
        effectiveFrom: today,
        reason: this.transloco.translate('settings.payrollRules.resetReason'),
      });
      this.items.update((list) =>
        list.map((x) => (x.ruleKey === updated.ruleKey ? updated : x)),
      );
      this.confirmedForLock.set(false);
      this.resetTarget.set(null);
      this.toast.show(this.transloco.translate('settings.payrollRules.toastReset'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('settings.payrollRules.toastFail'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  async applyOfficial(item: PayrollRuleListItem): Promise<void> {
    if (!item.officialDefaultJson) return;
    this.busy.set(true);
    try {
      const today = new Date().toISOString().slice(0, 10);
      const updated = await this.api.reset(item.ruleKey, {
        valueJson: item.officialDefaultJson,
        effectiveFrom: today,
        reason: this.transloco.translate('settings.payrollRules.applyUpdateReason'),
      });
      this.items.update((list) =>
        list.map((x) => (x.ruleKey === updated.ruleKey ? updated : x)),
      );
      this.confirmedForLock.set(false);
      this.toast.show(this.transloco.translate('settings.payrollRules.toastUpdated'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('settings.payrollRules.toastFail'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  async confirmRules(): Promise<void> {
    const reason = this.confirmReason().trim();
    if (!reason) {
      this.toast.show(this.transloco.translate('settings.payrollRules.toastValidation'), 'danger');
      return;
    }
    this.busy.set(true);
    try {
      const result = await this.api.confirm({ reason });
      this.confirmedForLock.set(result.rulesConfirmedForLock);
      this.confirmedAt.set(result.rulesConfirmedAt);
      this.confirmedReason.set(result.reason);
      this.confirmOpen.set(false);
      this.confirmReason.set('');
      this.toast.show(this.transloco.translate('settings.payrollRules.toastConfirmed'), 'success');
    } catch {
      this.toast.show(this.transloco.translate('settings.payrollRules.toastFail'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }
}
