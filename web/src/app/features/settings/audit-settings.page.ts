import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuditApi } from '../../core/api/audit.api';
import { AuditLogListItem } from '../../core/api/audit.models';
import { UsersApi, UserListItem } from '../../core/api/users.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import {
  AUDIT_ACTION_OPTIONS,
  AUDIT_ENTITY_OPTIONS,
  auditCategory,
  diffEntries,
  formatDateTime,
  isFailedLogin,
  isSystemActor,
  parseDevice,
  parseJsonRecord,
} from './audit-format';

type PageState = 'loading' | 'ready' | 'error' | 'forbidden';
type DatePreset = 'today' | '7d' | '30d' | 'custom';

@Component({
  selector: 'kadr-audit-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DrawerComponent,
  ],
  template: `
    @if (!canRead()) {
      <kadr-empty-state
        [title]="'settings.audit.noPermissionTitle' | transloco"
        [description]="'settings.audit.noPermissionBody' | transloco"
      >
      <a routerLink="/home">
          <kadr-button variant="secondary" type="button">
            {{ 'settings.audit.backHome' | transloco }}
          </kadr-button>
        </a>
      </kadr-empty-state>
    } @else if (state() === 'loading' && items().length === 0) {
      <div class="mx-auto flex w-full max-w-6xl flex-col gap-4">
        <kadr-skeleton height="2.5rem" width="50%" />
        <kadr-skeleton height="6rem" />
        <div class="flex flex-col gap-2" role="status" [attr.aria-label]="'ui.loading' | transloco">
          @for (_ of skeletonRows; track $index) {
            <kadr-skeleton height="3.5rem" />
          }
        </div>
      </div>
    } @else if (state() === 'error' && items().length === 0) {
      <kadr-empty-state
        [title]="'settings.audit.loadErrorTitle' | transloco"
        [description]="'settings.audit.loadErrorBody' | transloco"
      >
        <kadr-button variant="secondary" type="button" (click)="reload()">
          {{ 'home.retry' | transloco }}
        </kadr-button>
      </kadr-empty-state>
    } @else {
      <div class="mx-auto flex w-full max-w-6xl flex-col gap-6">

        <header class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div class="text-start">
            <h1 class="text-2xl font-bold leading-tight text-fg">
              {{ 'settings.audit.title' | transloco }}
            </h1>
            <p class="mt-1 text-sm text-fg-2">{{ 'settings.audit.subtitle' | transloco }}</p>
          </div>
          <kadr-button
            variant="secondary"
            type="button"
            [disabled]="exporting()"
            (click)="exportCsv()"
          >
            {{ 'settings.audit.exportCsv' | transloco }}
          </kadr-button>
        </header>

        <!-- Filters -->
        <section class="rounded-lg border border-line bg-surface p-4 shadow-sm">
          <div class="mb-3 flex items-center justify-between gap-2 lg:hidden">
            <kadr-button variant="secondary" type="button" (click)="filtersOpen.set(!filtersOpen())">
              {{ 'settings.audit.filtersToggle' | transloco }}
            </kadr-button>
          </div>

          <div class="flex flex-col gap-3" [class.hidden]="!filtersOpen() && isNarrow()">
            <div
              class="inline-flex flex-wrap rounded-md border border-line bg-subtle p-0.5"
              role="group"
              [attr.aria-label]="'settings.audit.datePresetAria' | transloco"
            >
              @for (p of presets; track p) {
                <button
                  type="button"
                  class="rounded-sm px-3 py-1.5 text-sm font-medium text-fg-2 hover:text-fg"
                  [class.bg-surface]="preset() === p"
                  [class.text-fg]="preset() === p"
                  [class.shadow-sm]="preset() === p"
                  (click)="setPreset(p)"
                >
                  {{ ('settings.audit.presets.' + p) | transloco }}
                </button>
              }
            </div>

            @if (preset() === 'custom') {
              <div class="grid gap-3 sm:grid-cols-2">
                <label class="flex flex-col gap-1 text-start text-sm">
                  <span class="text-fg-2">{{ 'settings.audit.from' | transloco }}</span>
                  <input
                    type="date"
                    class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
                    [ngModel]="customFrom()"
                    (ngModelChange)="customFrom.set($event); applyFilters()"
                  />
                </label>
                <label class="flex flex-col gap-1 text-start text-sm">
                  <span class="text-fg-2">{{ 'settings.audit.to' | transloco }}</span>
                  <input
                    type="date"
                    class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
                    [ngModel]="customTo()"
                    (ngModelChange)="customTo.set($event); applyFilters()"
                  />
                </label>
              </div>
            }

            <div class="grid gap-3 md:grid-cols-3">
              <label class="flex flex-col gap-1 text-start text-sm">
                <span class="text-fg-2">{{ 'settings.audit.filterUser' | transloco }}</span>
                <select
                  class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
                  [ngModel]="userId()"
                  (ngModelChange)="userId.set($event); applyFilters()"
                >
                  <option value="">{{ 'settings.audit.allUsers' | transloco }}</option>
                  @for (u of users(); track u.id) {
                    <option [value]="u.id">{{ u.fullName }}</option>
                  }
                </select>
              </label>
              <label class="flex flex-col gap-1 text-start text-sm">
                <span class="text-fg-2">{{ 'settings.audit.filterAction' | transloco }}</span>
                <select
                  class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
                  [ngModel]="action()"
                  (ngModelChange)="action.set($event); applyFilters()"
                >
                  <option value="">{{ 'settings.audit.allActions' | transloco }}</option>
                  @for (a of actionOptions; track a) {
                    <option [value]="a">{{ actionLabel(a) }}</option>
                  }
                </select>
              </label>
              <label class="flex flex-col gap-1 text-start text-sm">
                <span class="text-fg-2">{{ 'settings.audit.filterEntity' | transloco }}</span>
                <select
                  class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
                  [ngModel]="entityType()"
                  (ngModelChange)="entityType.set($event); applyFilters()"
                >
                  <option value="">{{ 'settings.audit.allEntities' | transloco }}</option>
                  @for (e of entityOptions; track e) {
                    <option [value]="e">{{ entityLabel(e) }}</option>
                  }
                </select>
              </label>
            </div>

            @if (chips().length) {
              <div class="flex flex-wrap items-center gap-2">
                @for (chip of chips(); track chip.key) {
                  <span
                    class="inline-flex items-center gap-1 rounded-md bg-subtle px-2 py-1 text-xs text-fg"
                  >
                    {{ chip.label }}
                    <button
                      type="button"
                      class="rounded-sm p-0.5 text-fg-2 hover:text-fg"
                      [attr.aria-label]="'settings.audit.removeChip' | transloco"
                      (click)="removeChip(chip.key)"
                    >
                      ×
                    </button>
                  </span>
                }
                <button
                  type="button"
                  class="text-sm font-medium text-accent underline-offset-2 hover:underline"
                  (click)="clearFilters()"
                >
                  {{ 'settings.audit.clearFilters' | transloco }}
                </button>
              </div>
            }
          </div>
        </section>

        @if (state() === 'error') {
          <div class="rounded-md border border-line bg-danger-soft px-4 py-3 text-sm text-fg" role="alert">
            <p>{{ 'settings.audit.loadErrorBody' | transloco }}</p>
            <kadr-button class="mt-2" variant="secondary" type="button" (click)="reload()">
              {{ 'home.retry' | transloco }}
            </kadr-button>
          </div>
        }

        @if (state() === 'ready' && items().length === 0) {
          <kadr-empty-state
            [title]="'settings.audit.emptyTitle' | transloco"
            [description]="'settings.audit.emptyBody' | transloco"
          >
            <kadr-button variant="primary" type="button" (click)="clearFilters()">
              {{ 'settings.audit.clearFiltersPrimary' | transloco }}
            </kadr-button>
            <kadr-button variant="secondary" type="button" (click)="focusDateRange()">
              {{ 'settings.audit.editDateRange' | transloco }}
            </kadr-button>
          </kadr-empty-state>
        } @else {
          <!-- Desktop table -->
          <div class="hidden overflow-x-auto rounded-lg border border-line bg-surface shadow-sm lg:block">
            <table class="w-full min-w-0 border-collapse text-sm">
              <thead class="border-b border-line bg-subtle text-start text-xs font-medium text-fg-2">
                <tr>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.audit.col.date' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.audit.col.actor' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.audit.col.action' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.audit.col.entity' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.audit.col.ip' | transloco }}</th>
                  <th class="px-3 py-2.5 font-medium">{{ 'settings.audit.col.details' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr class="border-b border-line last:border-0 hover:bg-subtle/60">
                    <td class="px-3 py-3 align-top tabular-nums text-fg">
                      <div>{{ datePart(item.at) }}</div>
                      <div class="text-xs text-fg-2">{{ timePart(item.at) }}</div>
                    </td>
                    <td class="px-3 py-3 align-top">
                      <div class="flex items-start gap-2">
                        <span
                          class="inline-flex size-8 shrink-0 items-center justify-center rounded-full bg-subtle text-xs font-medium text-fg"
                          aria-hidden="true"
                        >
                          @if (isSystem(item)) {
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="size-4">
                              <circle cx="12" cy="12" r="3" />
                              <path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
                            </svg>
                          } @else {
                            {{ initials(item) }}
                          }
                        </span>
                        <div class="min-w-0 text-start">
                          <div class="font-medium text-fg">{{ actorName(item) }}</div>
                          @if (item.userEmail) {
                            <div class="truncate text-xs text-fg-2" dir="ltr">{{ item.userEmail }}</div>
                          }
                        </div>
                      </div>
                    </td>
                    <td class="px-3 py-3 align-top text-start">
                      <div class="mb-1">
                        <kadr-badge
                          [tone]="categoryTone(item)"
                          [label]="categoryLabel(item)"
                        />
                      </div>
                      <p class="text-fg">{{ actionSentence(item) }}</p>
                    </td>
                    <td class="px-3 py-3 align-top text-start text-fg">
                      <span class="text-fg-2">{{ entityLabel(item.entityType) }}</span>
                      @if (item.entityDisplayName) {
                        <span class="text-fg-2"> · </span>
                        <span>{{ item.entityDisplayName }}</span>
                      }
                    </td>
                    <td class="px-3 py-3 align-top text-start">
                      @if (item.ipAddress) {
                        <div class="font-mono text-xs text-fg" dir="ltr">{{ item.ipAddress }}</div>
                      } @else {
                        <div class="text-xs text-fg-2">—</div>
                      }
                      @if (deviceLine(item); as device) {
                        <div class="text-xs text-fg-2">{{ device }}</div>
                      }
                    </td>
                    <td class="px-3 py-3 align-top">
                      <kadr-button variant="secondary" type="button" (click)="openDetails(item)">
                        {{ 'settings.audit.details' | transloco }}
                      </kadr-button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Mobile cards -->
          <div class="flex flex-col gap-3 lg:hidden">
            @for (item of items(); track item.id) {
              <article class="rounded-lg border border-line bg-surface p-4 shadow-sm text-start">
                <div class="flex items-start justify-between gap-2">
                  <div class="tabular-nums text-sm text-fg">
                    <div>{{ datePart(item.at) }}</div>
                    <div class="text-xs text-fg-2">{{ timePart(item.at) }}</div>
                  </div>
                  <kadr-badge [tone]="categoryTone(item)" [label]="categoryLabel(item)" />
                </div>
                <p class="mt-2 font-medium text-fg">{{ actionSentence(item) }}</p>
                <p class="mt-1 text-sm text-fg-2">{{ actorName(item) }}</p>
                @if (item.ipAddress) {
                  <p class="mt-1 font-mono text-xs text-fg-2" dir="ltr">{{ item.ipAddress }}</p>
                }
                <div class="mt-3">
                  <kadr-button variant="secondary" type="button" (click)="openDetails(item)">
                    {{ 'settings.audit.details' | transloco }}
                  </kadr-button>
                </div>
              </article>
            }
          </div>

          <footer class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p class="flex items-start gap-2 text-xs text-fg-2 text-start">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="mt-0.5 size-4 shrink-0" aria-hidden="true">
                <rect width="18" height="11" x="3" y="11" rx="2" />
                <path d="M7 11V7a5 5 0 0 1 10 0v4" />
              </svg>
              {{ 'settings.audit.privacyFooter' | transloco }}
            </p>
            <div class="flex gap-2">
              <kadr-button
                variant="secondary"
                type="button"
                [disabled]="!canPrev() || state() === 'loading'"
                (click)="prevPage()"
              >
                {{ 'settings.audit.prev' | transloco }}
              </kadr-button>
              <kadr-button
                variant="secondary"
                type="button"
                [disabled]="!nextCursor() || state() === 'loading'"
                (click)="nextPage()"
              >
                {{ 'settings.audit.next' | transloco }}
              </kadr-button>
            </div>
          </footer>
        }
      </div>
    }

    <kadr-drawer
      [open]="!!selected()"
      [title]="'settings.audit.drawerTitle' | transloco"
      [closeLabel]="'settings.audit.close' | transloco"
      size="lg"
      (closed)="selected.set(null)"
    >
      @if (selected(); as item) {
        <ng-container>
          <div class="flex flex-col gap-4 text-start">
            <div class="flex flex-wrap items-center gap-2">
              <code class="rounded-md bg-subtle px-2 py-1 font-mono text-xs text-fg" dir="ltr">{{
                item.id
              }}</code>
              <kadr-button variant="secondary" type="button" (click)="copyText(item.id)">
                {{ 'settings.audit.copy' | transloco }}
              </kadr-button>
              <kadr-badge [tone]="categoryTone(item)" [label]="categoryLabel(item)" />
            </div>
            <p class="text-base font-medium text-fg">{{ actionSentence(item) }}</p>

            <dl class="grid gap-3 text-sm">
              <div>
                <dt class="text-fg-2">{{ 'settings.audit.meta.actor' | transloco }}</dt>
                <dd class="mt-1 text-fg">
                  {{ actorName(item) }}
                  @if (item.userEmail) {
                    <span class="block text-xs text-fg-2" dir="ltr">{{ item.userEmail }}</span>
                  }
                </dd>
              </div>
              <div>
                <dt class="text-fg-2">{{ 'settings.audit.meta.when' | transloco }}</dt>
                <dd class="mt-1 tabular-nums text-fg">
                  {{ datePart(item.at) }} · {{ timePart(item.at) }}
                </dd>
              </div>
              <div>
                <dt class="text-fg-2">{{ 'settings.audit.meta.ip' | transloco }}</dt>
                <dd class="mt-1 font-mono text-fg" dir="ltr">{{ item.ipAddress || '—' }}</dd>
              </div>
              <div>
                <dt class="text-fg-2">{{ 'settings.audit.meta.device' | transloco }}</dt>
                <dd class="mt-1 text-fg">{{ deviceLine(item) || '—' }}</dd>
              </div>
              <div>
                <dt class="text-fg-2">{{ 'settings.audit.meta.entityType' | transloco }}</dt>
                <dd class="mt-1 text-fg">{{ entityLabel(item.entityType) }}</dd>
              </div>
              <div>
                <dt class="text-fg-2">{{ 'settings.audit.meta.entityId' | transloco }}</dt>
                <dd class="mt-1 flex flex-wrap items-center gap-2">
                  <code class="rounded-md bg-subtle px-2 py-1 font-mono text-xs" dir="ltr">{{
                    item.entityId || '—'
                  }}</code>
                  @if (item.entityId) {
                    <kadr-button variant="secondary" type="button" (click)="copyText(item.entityId!)">
                      {{ 'settings.audit.copy' | transloco }}
                    </kadr-button>
                  }
                </dd>
              </div>
            </dl>

            <section>
              <h3 class="text-sm font-bold text-fg">{{ 'settings.audit.changes' | transloco }}</h3>
              @if (diffs(item).length === 0) {
                <p class="mt-2 text-sm text-fg-2">{{ 'settings.audit.noChanges' | transloco }}</p>
              } @else {
                <div class="mt-2 overflow-x-auto rounded-md border border-line">
                  <table class="w-full text-sm">
                    <thead class="bg-subtle text-start text-xs text-fg-2">
                      <tr>
                        <th class="px-2 py-2">{{ 'settings.audit.diff.field' | transloco }}</th>
                        <th class="px-2 py-2">{{ 'settings.audit.diff.old' | transloco }}</th>
                        <th class="px-2 py-2"></th>
                        <th class="px-2 py-2">{{ 'settings.audit.diff.new' | transloco }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (row of diffs(item); track row.field) {
                        <tr class="border-t border-line">
                          <td class="px-2 py-2 align-top font-medium text-fg break-words">{{ row.field }}</td>
                          <td class="px-2 py-2 align-top">
                            <span
                              class="inline-block max-w-full break-words rounded-sm bg-danger-soft px-1.5 py-0.5 text-fg line-through"
                              >{{ row.oldValue }}</span
                            >
                          </td>
                          <td class="px-1 py-2 align-middle text-fg-2" aria-hidden="true">→</td>
                          <td class="px-2 py-2 align-top">
                            <span
                              class="inline-block max-w-full break-words rounded-sm bg-success-soft px-1.5 py-0.5 text-fg"
                              >{{ row.newValue }}</span
                            >
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </section>
          </div>
        </ng-container>
      }
      @if (selected()) {
        <div drawerFooter class="contents">
          <kadr-button variant="secondary" type="button" (click)="selected.set(null)">
            {{ 'settings.audit.close' | transloco }}
          </kadr-button>
        </div>
      }
    </kadr-drawer>
  `,
})
export class AuditSettingsPage implements OnInit {
  private readonly api = inject(AuditApi);
  private readonly usersApi = inject(UsersApi);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly skeletonRows = Array.from({ length: 8 });
  protected readonly presets: DatePreset[] = ['today', '7d', '30d', 'custom'];
  protected readonly actionOptions = AUDIT_ACTION_OPTIONS;
  protected readonly entityOptions = AUDIT_ENTITY_OPTIONS;

  protected readonly canRead = computed(() => this.auth.hasPermission(PermissionCodes.AuditRead));
  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<AuditLogListItem[]>([]);
  protected readonly users = signal<UserListItem[]>([]);
  protected readonly nextCursor = signal<string | null>(null);
  protected readonly cursorStack = signal<(string | null)[]>([null]);
  protected readonly exporting = signal(false);
  protected readonly selected = signal<AuditLogListItem | null>(null);
  protected readonly filtersOpen = signal(true);
  protected readonly isNarrow = signal(false);

  protected readonly preset = signal<DatePreset>('30d');
  protected readonly customFrom = signal('');
  protected readonly customTo = signal('');
  protected readonly userId = signal('');
  protected readonly action = signal('');
  protected readonly entityType = signal('');

  protected readonly chips = computed(() => {
    const list: { key: string; label: string }[] = [];
    list.push({
      key: 'preset',
      label: `${this.i18n.translate('settings.audit.chip.period')}: ${this.i18n.translate('settings.audit.presets.' + this.preset())}`,
    });
    if (this.userId()) {
      const u = this.users().find((x) => x.id === this.userId());
      list.push({
        key: 'user',
        label: `${this.i18n.translate('settings.audit.chip.user')}: ${u?.fullName ?? this.userId()}`,
      });
    }
    if (this.action()) {
      list.push({
        key: 'action',
        label: `${this.i18n.translate('settings.audit.chip.action')}: ${this.actionLabel(this.action())}`,
      });
    }
    if (this.entityType()) {
      list.push({
        key: 'entity',
        label: `${this.i18n.translate('settings.audit.chip.entity')}: ${this.entityLabel(this.entityType())}`,
      });
    }
    return list;
  });

  ngOnInit(): void {
    if (typeof window !== 'undefined') {
      const mq = window.matchMedia('(max-width: 1023px)');
      this.isNarrow.set(mq.matches);
      this.filtersOpen.set(!mq.matches);
      mq.addEventListener('change', (e) => {
        this.isNarrow.set(e.matches);
        if (!e.matches) {
          this.filtersOpen.set(true);
        }
      });
    }
    if (!this.canRead()) {
      this.state.set('forbidden');
      return;
    }
    void this.loadUsers();
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.cursorStack.set([null]);
    await this.loadPage(null);
  }

  protected applyFilters(): void {
    void this.reload();
  }

  protected clearFilters(): void {
    this.preset.set('30d');
    this.customFrom.set('');
    this.customTo.set('');
    this.userId.set('');
    this.action.set('');
    this.entityType.set('');
    void this.reload();
  }

  protected removeChip(key: string): void {
    if (key === 'preset') {
      this.preset.set('30d');
      this.customFrom.set('');
      this.customTo.set('');
    } else if (key === 'user') {
      this.userId.set('');
    } else if (key === 'action') {
      this.action.set('');
    } else if (key === 'entity') {
      this.entityType.set('');
    }
    void this.reload();
  }

  protected setPreset(p: DatePreset): void {
    this.preset.set(p);
    if (p !== 'custom') {
      void this.reload();
    }
  }

  protected focusDateRange(): void {
    this.filtersOpen.set(true);
    this.preset.set('custom');
  }

  protected canPrev(): boolean {
    return this.cursorStack().length > 1;
  }

  protected async nextPage(): Promise<void> {
    const cursor = this.nextCursor();
    if (!cursor) {
      return;
    }
    this.cursorStack.update((s) => [...s, cursor]);
    await this.loadPage(cursor);
  }

  protected async prevPage(): Promise<void> {
    if (!this.canPrev()) {
      return;
    }
    const stack = [...this.cursorStack()];
    stack.pop();
    this.cursorStack.set(stack);
    await this.loadPage(stack[stack.length - 1] ?? null);
  }

  protected openDetails(item: AuditLogListItem): void {
    this.selected.set(item);
  }

  protected datePart(iso: string): string {
    return formatDateTime(iso, this.i18n.getActiveLang()).date;
  }

  protected timePart(iso: string): string {
    return formatDateTime(iso, this.i18n.getActiveLang()).time;
  }

  protected deviceLine(item: AuditLogListItem): string {
    return parseDevice(item.userAgent);
  }

  protected isSystem(item: AuditLogListItem): boolean {
    return isSystemActor(item);
  }

  protected initials(item: AuditLogListItem): string {
    const name = item.userName?.trim() || item.userEmail || '?';
    const parts = name.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  }

  protected actorName(item: AuditLogListItem): string {
    if (isSystemActor(item)) {
      return this.i18n.translate('settings.audit.systemActor');
    }
    if (isFailedLogin(item) && !item.userName) {
      return this.i18n.translate('settings.audit.unknownActor');
    }
    return item.userName || item.userEmail || this.i18n.translate('settings.audit.unknownActor');
  }

  protected categoryLabel(item: AuditLogListItem): string {
    return this.i18n.translate(`settings.audit.categories.${auditCategory(item)}`);
  }

  protected categoryTone(item: AuditLogListItem): BadgeTone {
    if (isFailedLogin(item)) {
      return 'warning';
    }
    switch (auditCategory(item)) {
      case 'auth':
        return 'info';
      case 'users':
      case 'roles':
        return 'accent';
      case 'templates':
        return 'success';
      case 'invitations':
        return 'info';
      case 'subscription':
      case 'system':
        return 'neutral';
      default:
        return 'neutral';
    }
  }

  protected actionLabel(action: string): string {
    const key = `settings.audit.actions.${action}`;
    const t = this.i18n.translate(key);
    return t === key ? action : t;
  }

  protected entityLabel(entityType: string): string {
    const key = `settings.audit.entities.${entityType}`;
    const t = this.i18n.translate(key);
    return t === key ? entityType : t;
  }

  protected actionSentence(item: AuditLogListItem): string {
    const email =
      parseJsonRecord(item.newValuesJson)['email'] ??
      parseJsonRecord(item.oldValuesJson)['email'] ??
      item.userEmail ??
      '';
    const key = `settings.audit.sentences.${item.action}`;
    const translated = this.i18n.translate(key, {
      name: item.entityDisplayName || item.userName || '',
      email: String(email),
      entity: item.entityDisplayName || item.entityType,
    });
    if (translated !== key) {
      return translated;
    }
    return this.actionLabel(item.action);
  }

  protected diffs(item: AuditLogListItem) {
    return diffEntries(item.oldValuesJson, item.newValuesJson);
  }

  protected async copyText(value: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(value);
      this.toast.show(this.i18n.translate('settings.audit.copied'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.audit.copyFailed'), 'danger');
    }
  }

  protected async exportCsv(): Promise<void> {
    this.exporting.set(true);
    this.toast.show(this.i18n.translate('settings.audit.exportPreparing'), 'info', 8000);
    try {
      const range = this.dateRange();
      const blob = await this.api.exportCsv({
        userId: this.userId() || null,
        entityType: this.entityType() || null,
        action: this.action() || null,
        from: range.from,
        to: range.to,
      });
      const filename = `kadr-audit-${new Date().toISOString().slice(0, 10)}.csv`;
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      a.click();
      const kb = Math.max(1, Math.round(blob.size / 1024));
      this.toast.show(
        this.i18n.translate('settings.audit.exportDone', { name: filename, size: `${kb} KB` }),
        'success',
        10000,
        { label: this.i18n.translate('settings.audit.exportOpen'), url },
      );
    } catch {
      this.toast.show(this.i18n.translate('settings.audit.exportFailed'), 'danger');
    } finally {
      this.exporting.set(false);
    }
  }

  private async loadUsers(): Promise<void> {
    try {
      const page = await this.usersApi.list({ page: 1, pageSize: 100 });
      this.users.set(page.items);
    } catch {
      this.users.set([]);
    }
  }

  private async loadPage(cursor: string | null): Promise<void> {
    this.state.set('loading');
    try {
      const range = this.dateRange();
      const result = await this.api.list({
        userId: this.userId() || null,
        entityType: this.entityType() || null,
        action: this.action() || null,
        from: range.from,
        to: range.to,
        pageSize: 25,
        cursor,
      });
      this.items.set(result.items);
      this.nextCursor.set(result.nextCursor);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  private dateRange(): { from: string | null; to: string | null } {
    const now = new Date();
    const end = new Date(now);
    end.setHours(23, 59, 59, 999);
    const startOfDay = (d: Date) => {
      const x = new Date(d);
      x.setHours(0, 0, 0, 0);
      return x;
    };

    if (this.preset() === 'today') {
      return { from: startOfDay(now).toISOString(), to: end.toISOString() };
    }
    if (this.preset() === '7d') {
      const from = startOfDay(now);
      from.setDate(from.getDate() - 6);
      return { from: from.toISOString(), to: end.toISOString() };
    }
    if (this.preset() === '30d') {
      const from = startOfDay(now);
      from.setDate(from.getDate() - 29);
      return { from: from.toISOString(), to: end.toISOString() };
    }
    const from = this.customFrom()
      ? startOfDay(new Date(this.customFrom())).toISOString()
      : null;
    const to = this.customTo()
      ? (() => {
          const d = new Date(this.customTo());
          d.setHours(23, 59, 59, 999);
          return d.toISOString();
        })()
      : null;
    return { from, to };
  }
}
