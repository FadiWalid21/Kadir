import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { EmployeesApi, ExpiringDocumentsResult } from '../../core/api/employees.api';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';

type PageState = 'loading' | 'ready' | 'error' | 'empty' | 'locked';

@Component({
  selector: 'kadr-documents-expiring-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
  ],
  template: `
    <div class="flex flex-col gap-5">
      <header class="flex flex-wrap items-start justify-between gap-3">
        <div class="text-start">
          <h1 class="text-2xl font-bold text-fg">{{ 'employees.expiringTitle' | transloco }}</h1>
          <p class="mt-1 text-sm text-fg-2">{{ 'employees.expiringSubtitle' | transloco }}</p>
        </div>
        <a routerLink="/employees" class="contents">
          <kadr-button variant="secondary" type="button">{{
            'employees.backToList' | transloco
          }}</kadr-button>
        </a>
      </header>

      @if (state() === 'locked') {
        <kadr-empty-state
          [title]="'employees.featureLockedTitle' | transloco"
          [description]="'employees.featureLockedBody' | transloco"
        />
      } @else if (state() === 'loading') {
        @for (_ of [1, 2, 3]; track $index) {
          <kadr-skeleton height="3rem" />
        }
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
        @if (result(); as r) {
          <div class="flex flex-wrap gap-2 text-xs">
            <span class="rounded-full bg-subtle px-2 py-1">{{
              'employees.within60' | transloco: { n: r.within60 }
            }}</span>
            <span class="rounded-full bg-warning-soft px-2 py-1 text-warning">{{
              'employees.within30' | transloco: { n: r.within30 }
            }}</span>
            <span class="rounded-full bg-warning-soft px-2 py-1 text-warning">{{
              'employees.within7' | transloco: { n: r.within7 }
            }}</span>
            <span class="rounded-full bg-danger-soft px-2 py-1 text-danger">{{
              'employees.expiredCount' | transloco: { n: r.expired }
            }}</span>
          </div>
        }

        <div class="flex flex-wrap gap-1" role="tablist">
          @for (tab of windows; track tab.value) {
            <button
              type="button"
              role="tab"
              class="h-9 rounded-full px-3 text-xs"
              [class.bg-accent]="window() === tab.value"
              [class.text-on-accent]="window() === tab.value"
              [class.bg-subtle]="window() !== tab.value"
              [class.text-fg-2]="window() !== tab.value"
              (click)="setWindow(tab.value)"
            >
              {{ tab.labelKey | transloco }}
            </button>
          }
        </div>

        <div class="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4 sm:flex-row sm:items-end">
          <label class="grid min-w-0 flex-1 gap-1 text-xs text-fg-2">
            <span>{{ 'employees.search' | transloco }}</span>
            <input
              class="h-control rounded-md border border-line-strong bg-surface px-3 text-sm"
              [ngModel]="q()"
              (ngModelChange)="q.set($event)"
              (keyup.enter)="reload()"
            />
          </label>
          <kadr-button variant="secondary" type="button" (click)="reload()">{{
            'employees.apply' | transloco
          }}</kadr-button>
        </div>

        @if (state() === 'empty') {
          <kadr-empty-state
            [title]="'employees.expiringEmptyTitle' | transloco"
            [description]="'employees.expiringEmptyBody' | transloco"
          />
        } @else if (result(); as r) {
          <div class="overflow-x-auto rounded-lg border border-line bg-surface">
            <table class="w-full text-start text-sm">
              <thead class="border-b border-line bg-subtle text-xs text-fg-2">
                <tr>
                  <th class="px-3 py-3 font-medium">{{ 'glossary.employee' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.docType' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.docRef' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.docExpires' | transloco }}</th>
                  <th class="px-3 py-3 font-medium">{{ 'employees.colStatus' | transloco }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of r.items; track row.documentId) {
                  <tr class="border-b border-line">
                    <td class="px-3 py-3">
                      <a
                        [routerLink]="['/employees', row.employeeId]"
                        [queryParams]="{ tab: 'documents' }"
                        class="text-accent hover:underline"
                      >
                        {{ row.employeeNameAr }}
                      </a>
                      <p class="text-xs text-fg-muted">
                        {{ row.jobTitleNameAr || '—' }} · {{ row.departmentNameAr || '—' }}
                      </p>
                    </td>
                    <td class="px-3 py-3">{{ typeName(row) }}</td>
                    <td class="px-3 py-3"><bdi>{{ row.referenceNumber || '—' }}</bdi></td>
                    <td class="px-3 py-3 tabular-nums">{{ row.expiresOn || '—' }}</td>
                    <td class="px-3 py-3">
                      <kadr-badge
                        [tone]="expiryTone(row.expiryStatus)"
                        [label]="expiryLabel(row.expiryStatus, row.daysRemaining)"
                      />
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <div class="flex items-center justify-between gap-3 text-sm text-fg-2">
            <p>
              {{
                'employees.pageInfo'
                  | transloco: { from: pageFrom(), to: pageTo(), total: r.totalCount }
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
                [disabled]="pageTo() >= r.totalCount"
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
export class DocumentsExpiringPage implements OnInit {
  private readonly api = inject(EmployeesApi);
  private readonly i18n = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly result = signal<ExpiringDocumentsResult | null>(null);
  protected readonly window = signal('all');
  protected readonly q = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = 25;

  protected readonly windows = [
    { value: 'all', labelKey: 'employees.window.all' },
    { value: '60', labelKey: 'employees.window.d60' },
    { value: '30', labelKey: 'employees.window.d30' },
    { value: '7', labelKey: 'employees.window.d7' },
    { value: 'expired', labelKey: 'employees.window.expired' },
  ] as const;

  protected pageFrom(): number {
    const r = this.result();
    if (!r || r.totalCount === 0) return 0;
    return (this.page() - 1) * this.pageSize + 1;
  }

  protected pageTo(): number {
    const r = this.result();
    if (!r) return 0;
    return Math.min(this.page() * this.pageSize, r.totalCount);
  }

  ngOnInit(): void {
    void this.reload();
  }

  setWindow(value: string): void {
    this.window.set(value);
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

  typeName(row: ExpiringDocumentsResult['items'][number]): string {
    return this.i18n.getActiveLang() === 'en'
      ? row.documentTypeNameEn || row.documentTypeNameAr
      : row.documentTypeNameAr;
  }

  expiryTone(s: string): BadgeTone {
    if (s === 'expired') return 'danger';
    if (s === 'soon') return 'warning';
    return 'success';
  }

  expiryLabel(s: string, days: number | null): string {
    if (s === 'expired') return this.i18n.translate('employees.expiry.expired');
    if (days != null) {
      return this.i18n.translate('employees.daysLeft', { n: days });
    }
    return this.i18n.translate('employees.expiry.soon');
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      const result = await this.api.listExpiring({
        window: this.window(),
        q: this.q().trim() || undefined,
        page: this.page(),
        pageSize: this.pageSize,
      });
      this.result.set(result);
      this.state.set(result.items.length === 0 ? 'empty' : 'ready');
    } catch (err: unknown) {
      const code = (err as { error?: { code?: string } })?.error?.code;
      this.state.set(code === 'plan.feature_not_available' ? 'locked' : 'error');
    }
  }
}
