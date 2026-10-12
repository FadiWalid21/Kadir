import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ReportHubItem, ReportsApi } from '../../core/api/reports.api';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';

type PageState = 'loading' | 'ready' | 'error' | 'empty';

interface HubGroup {
  groupKey: string;
  items: ReportHubItem[];
}

@Component({
  selector: 'kadr-reports-hub-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslocoPipe,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
    IconComponent,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <header class="text-start">
        <h1 class="text-2xl font-bold text-fg">{{ 'reports.hub.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'reports.hub.subtitle' | transloco }}</p>
      </header>

      @if (state() === 'loading') {
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <kadr-skeleton height="7rem" />
          <kadr-skeleton height="7rem" />
          <kadr-skeleton height="7rem" />
        </div>
      } @else if (state() === 'error') {
        <kadr-network-error
          [title]="'reports.hub.loadErrorTitle' | transloco"
          [description]="'reports.hub.loadErrorBody' | transloco"
          (retry)="reload()"
        />
      } @else if (state() === 'empty') {
        <kadr-empty-state
          [title]="'reports.hub.emptyTitle' | transloco"
          [description]="'reports.hub.emptyBody' | transloco"
        />
      } @else {
        @for (group of groups(); track group.groupKey) {
          <section class="flex flex-col gap-3">
            <h2 class="text-sm font-medium text-fg-2">{{ group.groupKey | transloco }}</h2>
            <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
              @for (item of group.items; track item.key) {
                @if (item.available) {
                  <a
                    [routerLink]="item.route"
                    class="flex flex-col gap-2 rounded-lg border border-line bg-surface p-4 text-start shadow-sm transition-colors duration-150 ease-out hover:bg-hover focus-visible:outline-none"
                  >
                    <span class="inline-flex size-10 items-center justify-center rounded-md bg-accent-soft text-accent-soft-fg">
                      <kadr-icon name="chart" size="sm" />
                    </span>
                    <span class="text-base font-medium text-fg">{{ item.titleKey | transloco }}</span>
                    <span class="text-xs text-fg-muted">{{ 'reports.hub.open' | transloco }}</span>
                  </a>
                } @else {
                  <div
                    class="flex flex-col gap-2 rounded-lg border border-line bg-subtle p-4 text-start opacity-70"
                    [attr.aria-disabled]="true"
                  >
                    <span class="inline-flex size-10 items-center justify-center rounded-md bg-surface text-fg-muted">
                      <kadr-icon name="chart" size="sm" />
                    </span>
                    <span class="text-base font-medium text-fg">{{ item.titleKey | transloco }}</span>
                    <span class="text-xs text-fg-muted">{{ 'reports.hub.comingSoon' | transloco }}</span>
                  </div>
                }
              }
            </div>
          </section>
        }
      }
    </div>
  `,
})
export class ReportsHubPage implements OnInit {
  private readonly api = inject(ReportsApi);
  private readonly i18n = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<ReportHubItem[]>([]);

  protected readonly groups = computed((): HubGroup[] => {
    const map = new Map<string, ReportHubItem[]>();
    for (const item of this.items()) {
      const list = map.get(item.groupKey) ?? [];
      list.push(item);
      map.set(item.groupKey, list);
    }
    return [...map.entries()].map(([groupKey, groupItems]) => ({ groupKey, items: groupItems }));
  });

  ngOnInit(): void {
    document.title = this.i18n.translate('reports.hub.title');
    this.reload();
  }

  protected reload(): void {
    this.state.set('loading');
    void this.api
      .listHub()
      .then((rows) => {
        this.items.set(rows);
        this.state.set(rows.length === 0 ? 'empty' : 'ready');
      })
      .catch(() => this.state.set('error'));
  }
}
