import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PlatformApi } from '../../core/api/platform.api';
import { PlatformAuditLogItem } from '../../core/api/platform.models';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { DateFmtPipe } from './date-fmt.pipe';

type PageState = 'loading' | 'ready' | 'error' | 'empty';
type AuditTab = 'tenant' | 'platform';
type Period = 'today' | '7d' | '30d' | 'custom';

@Component({
  selector: 'kadr-platform-audit-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DateFmtPipe,
    AvatarComponent,
    BadgeComponent,
    IconComponent,
    DrawerComponent,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <header class="flex flex-col gap-2 text-start">
        <h1 class="text-2xl font-bold text-fg">{{ 'platform.audit.title' | transloco }}</h1>
        <p class="text-sm text-fg-2">{{ 'platform.audit.subtitle' | transloco }}</p>
      </header>

      <div class="flex flex-wrap gap-1 border-b border-line" role="tablist">
        <button
          type="button"
          role="tab"
          class="h-10 px-3 text-sm focus-visible:outline-none"
          [class.text-accent]="tab() === 'tenant'"
          [class.border-b-2]="tab() === 'tenant'"
          [class.border-accent]="tab() === 'tenant'"
          [class.text-fg-2]="tab() !== 'tenant'"
          (click)="setTab('tenant')"
        >
          {{ 'platform.audit.tabTenant' | transloco }}
        </button>
        <button
          type="button"
          role="tab"
          class="h-10 px-3 text-sm focus-visible:outline-none"
          [class.text-accent]="tab() === 'platform'"
          [class.border-b-2]="tab() === 'platform'"
          [class.border-accent]="tab() === 'platform'"
          [class.text-fg-2]="tab() !== 'platform'"
          (click)="setTab('platform')"
        >
          {{ 'platform.audit.tabPlatform' | transloco }}
        </button>
      </div>

      <div class="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4">
        <div
          class="inline-flex flex-wrap rounded-md border border-line bg-subtle p-0.5"
          role="group"
        >
          @for (p of periods; track p) {
            <button
              type="button"
              class="h-9 rounded-sm px-3 text-xs focus-visible:outline-none"
              [class.bg-accent]="period() === p"
              [class.text-on-accent]="period() === p"
              [class.text-fg-2]="period() !== p"
              (click)="setPeriod(p)"
            >
              {{ ('platform.audit.period.' + p) | transloco }}
            </button>
          }
        </div>
        @if (period() === 'custom') {
          <div class="flex flex-wrap gap-3">
            <label class="grid gap-1 text-xs text-fg-2">
              <span>{{ 'platform.audit.from' | transloco }}</span>
              <input
                type="date"
                class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm"
                [ngModel]="fromDate()"
                (ngModelChange)="fromDate.set($event)"
              />
            </label>
            <label class="grid gap-1 text-xs text-fg-2">
              <span>{{ 'platform.audit.to' | transloco }}</span>
              <input
                type="date"
                class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm"
                [ngModel]="toDate()"
                (ngModelChange)="toDate.set($event)"
              />
            </label>
          </div>
        }
        <div class="flex flex-col gap-3 md:flex-row md:flex-wrap md:items-end">
          <label class="grid min-w-0 flex-1 gap-1 text-xs text-fg-2">
            <span>{{ 'platform.audit.filterTenant' | transloco }}</span>
            <input
              class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm text-fg"
              [ngModel]="tenantId()"
              (ngModelChange)="tenantId.set($event)"
            />
          </label>
          <label class="grid min-w-0 flex-1 gap-1 text-xs text-fg-2">
            <span>{{ 'platform.audit.filterAction' | transloco }}</span>
            <input
              class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm text-fg"
              [ngModel]="action()"
              (ngModelChange)="action.set($event)"
            />
          </label>
          <kadr-button variant="secondary" type="button" (click)="reload()">
            {{ 'platform.audit.apply' | transloco }}
          </kadr-button>
          <kadr-button variant="secondary" type="button" (click)="clearFilters()">
            {{ 'platform.companies.clearFilters' | transloco }}
          </kadr-button>
        </div>
      </div>

      @if (state() === 'loading' && items().length === 0) {
        <div class="flex flex-col gap-2" role="status" [attr.aria-label]="'ui.loading' | transloco">
          @for (_ of [1, 2, 3, 4, 5]; track $index) {
            <kadr-skeleton height="3.5rem" />
          }
        </div>
      } @else if (state() === 'error' && items().length === 0) {
        <kadr-empty-state
          [title]="'platform.audit.loadErrorTitle' | transloco"
          [description]="'platform.audit.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">
            {{ 'home.retry' | transloco }}
          </kadr-button>
        </kadr-empty-state>
      } @else if (state() === 'empty') {
        <kadr-empty-state
          [title]="'platform.audit.emptyTitle' | transloco"
          [description]="'platform.audit.emptyBody' | transloco"
        />
      } @else {
        <div class="overflow-x-auto rounded-lg border border-line bg-surface">
          <table class="w-full text-start text-sm">
            <thead class="border-b border-line bg-subtle text-xs text-fg-2">
              <tr>
                <th class="px-3 py-3 font-medium">{{ 'platform.audit.colWhen' | transloco }}</th>
                <th class="px-3 py-3 font-medium">{{ 'platform.audit.colTenant' | transloco }}</th>
                <th class="px-3 py-3 font-medium">
                  {{
                    (tab() === 'platform' ? 'platform.audit.colActor' : 'platform.audit.colActor')
                      | transloco
                  }}
                </th>
                <th class="px-3 py-3 font-medium">{{ 'platform.audit.colAction' | transloco }}</th>
                @if (tab() === 'tenant') {
                  <th class="px-3 py-3 font-medium">{{ 'platform.audit.colIp' | transloco }}</th>
                } @else {
                  <th class="px-3 py-3 font-medium">{{ 'platform.detail.colReason' | transloco }}</th>
                }
                <th class="px-3 py-3 font-medium"></th>
              </tr>
            </thead>
            <tbody>
              @for (row of items(); track row.id) {
                <tr
                  class="border-b border-line hover:bg-hover"
                  [class.bg-warning-soft]="isFailedLogin(row)"
                >
                  <td class="px-3 py-3">
                    <span class="block tabular-nums text-fg">{{ datePart(row.at) }}</span>
                    <span class="block tabular-nums text-xs text-fg-muted">{{ timePart(row.at) }}</span>
                  </td>
                  <td class="px-3 py-3">
                    <div class="flex items-center gap-2">
                      <kadr-avatar
                        [name]="row.tenantName || row.tenantSlug || '—'"
                        size="sm"
                        [ariaLabel]="row.tenantName || ''"
                      />
                      <div class="min-w-0">
                        <p class="truncate text-fg">{{ row.tenantName || '—' }}</p>
                        @if (row.tenantSlug) {
                          <p class="text-xs text-fg-muted"><bdi>{{ row.tenantSlug }}</bdi></p>
                        }
                      </div>
                    </div>
                  </td>
                  <td class="px-3 py-3 text-fg">
                    {{ row.actorName || row.actorEmail || ('platform.audit.unknownActor' | transloco) }}
                    @if (row.actorEmail && row.actorName) {
                      <p class="text-xs text-fg-muted"><bdi>{{ row.actorEmail }}</bdi></p>
                    }
                  </td>
                  <td class="px-3 py-3 text-fg">
                    <span class="inline-flex flex-wrap items-center gap-1">
                      @if (isFailedLogin(row)) {
                        <kadr-icon name="alert" size="sm" class="text-warning" />
                        <kadr-badge
                          tone="warning"
                          [label]="'platform.detail.failedLogin' | transloco"
                        />
                      }
                      {{ row.action }}
                    </span>
                  </td>
                  @if (tab() === 'tenant') {
                    <td class="px-3 py-3 text-fg-2"><bdi>{{ row.ipAddress || '—' }}</bdi></td>
                  } @else {
                    <td class="px-3 py-3 text-fg-2">{{ reasonOf(row) || '—' }}</td>
                  }
                  <td class="px-3 py-3">
                    <button
                      type="button"
                      class="inline-flex size-9 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
                      [attr.aria-label]="'platform.audit.details' | transloco"
                      (click)="selected.set(row)"
                    >
                      <kadr-icon name="chevron-end" size="sm" [rtlFlip]="true" />
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <div class="flex flex-wrap items-center justify-between gap-3 text-sm text-fg-2">
          <p>{{ 'platform.audit.pageRange' | transloco: { from: 1, to: items().length } }}</p>
          <div class="flex gap-2">
            @if (nextCursor()) {
              <kadr-button
                variant="secondary"
                type="button"
                [loading]="loadingMore()"
                (click)="loadMore()"
              >
                {{ 'platform.audit.loadMore' | transloco }}
              </kadr-button>
            }
          </div>
        </div>
        <p class="flex items-start gap-2 text-xs text-fg-muted">
          <kadr-icon name="lock" size="sm" />
          {{ 'platform.audit.privacy' | transloco }}
        </p>
      }
    </div>

    <kadr-drawer
      [open]="!!selected()"
      [title]="'platform.audit.detailsTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="selected.set(null)"
    >
      @if (selected(); as e) {
        <dl class="flex flex-col gap-3 text-sm">
          <div>
            <dt class="text-xs text-fg-muted">{{ 'platform.audit.colWhen' | transloco }}</dt>
            <dd class="tabular-nums text-fg">{{ e.at | dateFmt }}</dd>
          </div>
          <div>
            <dt class="text-xs text-fg-muted">{{ 'platform.audit.colTenant' | transloco }}</dt>
            <dd class="text-fg">{{ e.tenantName || e.tenantSlug || '—' }}</dd>
          </div>
          <div>
            <dt class="text-xs text-fg-muted">{{ 'platform.audit.colActor' | transloco }}</dt>
            <dd class="text-fg">{{ e.actorName || e.actorEmail || '—' }}</dd>
          </div>
          <div>
            <dt class="text-xs text-fg-muted">{{ 'platform.audit.colAction' | transloco }}</dt>
            <dd class="text-fg">{{ e.action }}</dd>
          </div>
          <div>
            <dt class="text-xs text-fg-muted">{{ 'platform.audit.colEntity' | transloco }}</dt>
            <dd class="text-fg">{{ e.entityType }}</dd>
          </div>
          @if (e.ipAddress) {
            <div>
              <dt class="text-xs text-fg-muted">{{ 'platform.audit.colIp' | transloco }}</dt>
              <dd class="text-fg"><bdi>{{ e.ipAddress }}</bdi></dd>
            </div>
          }
          @if (reasonOf(e)) {
            <div>
              <dt class="text-xs text-fg-muted">{{ 'platform.detail.colReason' | transloco }}</dt>
              <dd class="text-fg">{{ reasonOf(e) }}</dd>
            </div>
          }
          <p class="text-xs text-fg-muted">{{ 'platform.audit.noDiff' | transloco }}</p>
        </dl>
      }
    </kadr-drawer>
  `,
})
export class PlatformAuditPage implements OnInit {
  private readonly api = inject(PlatformApi);
  private readonly i18n = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<PlatformAuditLogItem[]>([]);
  protected readonly nextCursor = signal<string | null>(null);
  protected readonly tab = signal<AuditTab>('tenant');
  protected readonly period = signal<Period>('7d');
  protected readonly action = signal('');
  protected readonly tenantId = signal('');
  protected readonly fromDate = signal('');
  protected readonly toDate = signal('');
  protected readonly loadingMore = signal(false);
  protected readonly selected = signal<PlatformAuditLogItem | null>(null);
  protected readonly periods: Period[] = ['today', '7d', '30d', 'custom'];

  ngOnInit(): void {
    void this.reload();
  }

  setTab(t: AuditTab): void {
    this.tab.set(t);
    void this.load(true);
  }

  setPeriod(p: Period): void {
    this.period.set(p);
    if (p !== 'custom') {
      void this.load(true);
    }
  }

  reload(): void {
    void this.load(true);
  }

  clearFilters(): void {
    this.action.set('');
    this.tenantId.set('');
    this.period.set('7d');
    this.fromDate.set('');
    this.toDate.set('');
    void this.load(true);
  }

  loadMore(): void {
    void this.load(false);
  }

  isFailedLogin(row: PlatformAuditLogItem): boolean {
    const a = row.action.toLowerCase();
    return a.includes('login.failed') || a.includes('failed_login') || a.includes('auth.login_failed');
  }

  reasonOf(row: PlatformAuditLogItem): string | null {
    const json = row.newValuesJson;
    if (!json) {
      return null;
    }
    try {
      const parsed = JSON.parse(json) as { reason?: string };
      return parsed.reason ?? null;
    } catch {
      return null;
    }
  }

  datePart(iso: string): string {
    try {
      return new Intl.DateTimeFormat(this.i18n.getActiveLang() === 'ar' ? 'ar' : 'en', {
        dateStyle: 'medium',
      }).format(new Date(iso));
    } catch {
      return iso;
    }
  }

  timePart(iso: string): string {
    try {
      return new Intl.DateTimeFormat(this.i18n.getActiveLang() === 'ar' ? 'ar' : 'en', {
        timeStyle: 'short',
      }).format(new Date(iso));
    } catch {
      return '';
    }
  }

  private range(): { from?: string; to?: string } {
    const now = new Date();
    const to = now.toISOString();
    if (this.period() === 'today') {
      const start = new Date(now);
      start.setUTCHours(0, 0, 0, 0);
      return { from: start.toISOString(), to };
    }
    if (this.period() === '7d') {
      const start = new Date(now.getTime() - 7 * 86400000);
      return { from: start.toISOString(), to };
    }
    if (this.period() === '30d') {
      const start = new Date(now.getTime() - 30 * 86400000);
      return { from: start.toISOString(), to };
    }
    return {
      from: this.fromDate() ? new Date(this.fromDate()).toISOString() : undefined,
      to: this.toDate() ? new Date(this.toDate() + 'T23:59:59Z').toISOString() : undefined,
    };
  }

  private async load(reset: boolean): Promise<void> {
    if (reset) {
      this.state.set('loading');
      this.items.set([]);
      this.nextCursor.set(null);
    } else {
      this.loadingMore.set(true);
    }
    try {
      const { from, to } = this.range();
      const result = await this.api.listAuditLogs({
        action: this.action().trim() || undefined,
        tenantId: this.tenantId().trim() || undefined,
        from,
        to,
        cursor: reset ? undefined : (this.nextCursor() ?? undefined),
        pageSize: 50,
      });
      const filtered =
        this.tab() === 'platform'
          ? result.items.filter((i) => i.actorKind === 'platform_admin')
          : result.items.filter((i) => i.actorKind !== 'platform_admin');
      this.items.update((prev) => (reset ? filtered : [...prev, ...filtered]));
      this.nextCursor.set(result.nextCursor);
      this.state.set(this.items().length === 0 ? 'empty' : 'ready');
    } catch {
      if (reset) {
        this.state.set('error');
      }
    } finally {
      this.loadingMore.set(false);
    }
  }
}
