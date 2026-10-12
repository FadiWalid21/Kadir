import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AttendanceApi, PagedCorrections } from '../../core/api/attendance.api';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { CorrectionRequestDrawerComponent } from './correction-request-drawer.component';

type PageState = 'loading' | 'ready' | 'error';
type Tab = '' | 'Pending' | 'Approved' | 'Rejected';

@Component({
  selector: 'kadr-my-requests-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    BadgeComponent,
    ButtonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
    CorrectionRequestDrawerComponent,
  ],
  template: `

    <header class="mb-4 flex flex-wrap items-start justify-between gap-3 text-start">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.requests.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.requests.subtitle' | transloco }}</p>
      </div>
      <kadr-button variant="primary" type="button" (click)="openForm()">
        {{ 'attendance.corrections.new' | transloco }}
      </kadr-button>
    </header>

    <div class="mb-4 flex flex-wrap gap-1" role="tablist">
      @for (t of tabs; track t) {
        <button
          type="button"
          role="tab"
          class="rounded-md px-3 py-2 text-sm font-medium"
          [class.bg-accent]="tab() === t"
          [class.text-on-accent]="tab() === t"
          [class.text-fg-2]="tab() !== t"
          [class.hover:bg-hover]="tab() !== t"
          (click)="setTab(t)"
        >
          {{ tabLabel(t) }}
        </button>
      }
    </div>

    @if (state() === 'loading') {
      <div class="flex flex-col gap-2">
        <kadr-skeleton height="4.5rem" />
        <kadr-skeleton height="4.5rem" />
        <kadr-skeleton height="4.5rem" />
      </div>
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.requests.loadErrorTitle' | transloco"
        [description]="'attendance.requests.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (page(); as p) {
      @if (p.items.length === 0) {
        <kadr-empty-state
          [title]="'attendance.requests.emptyTitle' | transloco"
          [description]="'attendance.requests.emptyBody' | transloco"
        />
      } @else {
        <ul class="flex flex-col gap-2">
          @for (item of p.items; track item.id) {
            <li class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
              <div class="flex items-start justify-between gap-2">
                <div>
                  <p class="text-sm font-medium text-fg">
                    {{ ('attendance.corrections.types.' + item.problemType) | transloco }}
                  </p>
                  <p class="mt-0.5 text-xs tabular-nums text-fg-muted">{{ item.workDate }}</p>
                </div>
                <kadr-badge [tone]="statusTone(item.status)" [label]="statusLabel(item.status)" />
              </div>
              <p class="mt-2 text-sm text-fg-2">{{ item.reason }}</p>
              @if (item.rejectReason) {
                <p class="mt-2 text-sm text-danger">
                  {{ 'attendance.corrections.rejectReason' | transloco }}: {{ item.rejectReason }}
                </p>
              }
            </li>
          }
        </ul>
      }
    }

    <kadr-correction-request-drawer
      [open]="formOpen()"
      [initialDate]="formDate()"
      (closed)="formOpen.set(false)"
      (submitted)="reload()"
    />
  `,
})
export class MyRequestsPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly drawer = viewChild(CorrectionRequestDrawerComponent);

  protected readonly tabs: Tab[] = ['', 'Pending', 'Approved', 'Rejected'];
  protected readonly state = signal<PageState>('loading');
  protected readonly page = signal<PagedCorrections | null>(null);
  protected readonly tab = signal<Tab>('');
  protected readonly formOpen = signal(false);
  protected readonly formDate = signal<string | null>(null);

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.requests.title'));
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const data = await this.api.listMyCorrections(this.tab() || undefined);
      this.page.set(data);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected setTab(t: Tab): void {
    this.tab.set(t);
    void this.reload();
  }

  protected openForm(date?: string): void {
    this.formDate.set(date ?? null);
    this.formOpen.set(true);
    queueMicrotask(() => this.drawer()?.resetFromInputs());
  }

  protected tabLabel(t: Tab): string {
    if (!t) return this.transloco.translate('attendance.requests.tabAll');
    return this.statusLabel(t);
  }

  protected statusLabel(status: string): string {
    const key = `ui.status${status}`;
    const t = this.transloco.translate(key);
    return t === key ? status : t;
  }

  protected statusTone(status: string): BadgeTone {
    switch (status) {
      case 'Approved':
        return 'success';
      case 'Rejected':
        return 'danger';
      case 'Pending':
        return 'warning';
      default:
        return 'neutral';
    }
  }
}
