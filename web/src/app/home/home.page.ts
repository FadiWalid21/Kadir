import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  isDevMode,
  OnInit,
  signal,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { HomeApi, HomeDashboardDto } from '../core/api/home.api';
import { AuthService } from '../core/auth/auth.service';
import { BadgeComponent } from '../shared/ui/badge/badge.component';
import { ButtonComponent } from '../shared/ui/button/button.component';
import { EmptyStateComponent } from '../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { NetworkErrorComponent } from '../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../shared/ui/skeleton/skeleton.component';
import { HomeAttendanceCardComponent } from '../features/attendance/home-attendance-card.component';

type HomeState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    RouterLink,
    ButtonComponent,
    BadgeComponent,
    EmptyStateComponent,
    SkeletonComponent,
    IconComponent,
    NetworkErrorComponent,
    HomeAttendanceCardComponent,
  ],
  template: `
    @if (state() === 'loading') {
      <div class="flex flex-col gap-6" role="status" [attr.aria-label]="'ui.loading' | transloco">
        <kadr-skeleton height="2.25rem" width="55%" />
        <kadr-skeleton height="1.25rem" width="40%" />
        <kadr-skeleton height="12rem" />
        <div class="grid gap-4 md:grid-cols-2">
          <kadr-skeleton height="8rem" />
          <kadr-skeleton height="8rem" />
        </div>
      </div>
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'home.loadErrorTitle' | transloco"
        [description]="'home.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (dashboard(); as d) {
      <header class="mb-8 flex flex-col gap-2 text-start">
        <h1 class="text-2xl font-bold leading-tight text-fg md:text-3xl">
          {{ welcomeTitle() }}
        </h1>
        <p class="max-w-2xl text-sm text-fg-2">{{ welcomeSubtitle() }}</p>
      </header>

      @if (d.checklist; as checklist) {
        <section
          class="mb-6 rounded-lg border border-line bg-surface p-5 shadow-sm"
          [attr.aria-label]="'home.checklistTitle' | transloco"
        >
          <div class="mb-4 flex flex-wrap items-baseline justify-between gap-2">
            <div class="text-start">
              <h2 class="text-lg font-bold text-fg">{{ 'home.checklistTitle' | transloco }}</h2>
              <p class="mt-1 text-sm text-fg-2">{{ 'home.checklistIntro' | transloco }}</p>
            </div>
            <p class="text-sm font-medium tabular-nums text-fg-muted">
              {{
                'home.checklistProgress'
                  | transloco: { done: checklist.doneCount, total: checklist.totalCount }
              }}
            </p>
          </div>
          <ul class="flex flex-col gap-2">
            @for (item of checklist.items; track item.key) {
              <li
                class="flex items-center justify-between gap-3 rounded-md border border-line bg-app px-4 py-3"
              >
                <a
                  [routerLink]="item.linkPath"
                  class="text-sm text-accent underline-offset-2 hover:underline"
                >
                  {{ checklistLabel(item.key) }}
                </a>
                @if (item.done) {
                  <span class="inline-flex items-center gap-1 text-sm text-success">
                    <kadr-icon name="check" size="sm" />
                    {{ 'home.checklistDone' | transloco }}
                  </span>
                } @else {
                  <kadr-badge tone="neutral" [label]="'home.checklistAction' | transloco" />
                }
              </li>
            }
          </ul>
        </section>
      }

      @if (d.hr; as hr) {
        <section class="mb-6 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <article class="rounded-lg border border-line bg-surface p-4 shadow-sm">
            <p class="text-xs text-fg-muted">{{ 'home.hr.headcount' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ hr.headcount }}</p>
          </article>
          <article class="rounded-lg border border-line bg-surface p-4 shadow-sm">
            <p class="text-xs text-fg-muted">{{ 'home.hr.lateToday' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ hr.lateToday }}</p>
          </article>
          <article class="rounded-lg border border-line bg-surface p-4 shadow-sm">
            <p class="text-xs text-fg-muted">{{ 'home.hr.absentToday' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ hr.absentToday }}</p>
          </article>
          <article class="rounded-lg border border-line bg-surface p-4 shadow-sm">
            <p class="text-xs text-fg-muted">{{ 'home.hr.pending' | transloco }}</p>
            <p class="mt-1 text-2xl font-bold tabular-nums text-fg">{{ hr.pendingWorkflowCount }}</p>
          </article>
        </section>
      }

      <div class="grid gap-4 lg:grid-cols-2">
        @if (d.manager; as manager) {
          <section class="rounded-lg border border-line bg-surface p-5 shadow-sm">
            <div class="mb-3 flex items-baseline justify-between gap-2">
              <h2 class="text-base font-bold text-fg">{{ 'home.widgetApprovalsTitle' | transloco }}</h2>
              <span class="text-xs tabular-nums text-fg-muted">{{ manager.pendingApprovalCount }}</span>
            </div>
            @if (manager.pendingApprovals.length === 0) {
              <kadr-empty-state
                [title]="'home.widgetEmptyTitle' | transloco"
                [description]="'home.widgetEmptyBody' | transloco"
              />
            } @else {
              <ul class="flex flex-col gap-2">
                @for (a of manager.pendingApprovals; track a.taskId) {
                  <li class="rounded-md border border-line bg-app px-3 py-2 text-start">
                    <p class="text-sm font-medium text-fg">
                      {{ nameOf(a.employeeNameAr, a.employeeNameEn) }}
                    </p>
                    <p class="text-xs text-fg-2">{{ a.requestType }}</p>
                  </li>
                }
              </ul>
              <a
                routerLink="/leave/approvals"
                class="mt-3 inline-flex text-sm text-accent underline-offset-2 hover:underline"
              >
                {{ 'home.viewApprovals' | transloco }}
              </a>
            }
            <div class="mt-4 grid grid-cols-4 gap-2 border-t border-line pt-3 text-center">
              <div>
                <p class="text-lg font-bold tabular-nums text-fg">{{ manager.teamToday.present }}</p>
                <p class="text-xs text-fg-muted">{{ 'home.team.present' | transloco }}</p>
              </div>
              <div>
                <p class="text-lg font-bold tabular-nums text-fg">{{ manager.teamToday.late }}</p>
                <p class="text-xs text-fg-muted">{{ 'home.team.late' | transloco }}</p>
              </div>
              <div>
                <p class="text-lg font-bold tabular-nums text-fg">{{ manager.teamToday.absent }}</p>
                <p class="text-xs text-fg-muted">{{ 'home.team.absent' | transloco }}</p>
              </div>
              <div>
                <p class="text-lg font-bold tabular-nums text-fg">{{ manager.teamToday.onLeave }}</p>
                <p class="text-xs text-fg-muted">{{ 'home.team.onLeave' | transloco }}</p>
              </div>
            </div>
          </section>
        }

        @if (d.employee; as employee) {
          <section
            class="rounded-lg border border-line bg-surface p-5 shadow-sm"
            [attr.aria-label]="'home.widgetAttendanceTitle' | transloco"
          >
            <h2 class="mb-3 text-base font-bold text-fg">
              {{ 'home.widgetAttendanceTitle' | transloco }}
            </h2>
            <kadr-home-attendance-card />
            @if (employee.leaveBalances.length > 0) {
              <div class="mt-4 border-t border-line pt-3">
                <h3 class="mb-2 text-sm font-bold text-fg">{{ 'home.balancesTitle' | transloco }}</h3>
                <ul class="flex flex-col gap-1">
                  @for (b of employee.leaveBalances; track b.leaveTypeId) {
                    <li class="flex justify-between gap-2 text-sm">
                      <span class="text-fg-2">{{ nameOf(b.nameAr, b.nameEn) }}</span>
                      <span class="tabular-nums text-fg">{{ b.remaining }} / {{ b.entitled }}</span>
                    </li>
                  }
                </ul>
              </div>
            }
          </section>
        } @else if (!d.manager) {
          <section
            class="rounded-lg border border-line bg-surface p-5 shadow-sm"
            [attr.aria-label]="'home.widgetAttendanceTitle' | transloco"
          >
            <h2 class="mb-3 text-base font-bold text-fg">
              {{ 'home.widgetAttendanceTitle' | transloco }}
            </h2>
            <kadr-home-attendance-card />
          </section>
        }

        @if (d.hr?.expiringDocuments?.length) {
          <section class="rounded-lg border border-line bg-surface p-5 shadow-sm lg:col-span-2">
            <h2 class="mb-3 text-base font-bold text-fg">{{ 'home.hr.expiring' | transloco }}</h2>
            <ul class="flex flex-col gap-2">
              @for (doc of d.hr!.expiringDocuments; track doc.documentId) {
                <li class="flex flex-wrap items-center justify-between gap-2 rounded-md border border-line bg-app px-3 py-2">
                  <div class="text-start">
                    <p class="text-sm font-medium text-fg">
                      {{ nameOf(doc.employeeNameAr, doc.employeeNameEn) }}
                    </p>
                    <p class="text-xs text-fg-2">
                      {{ nameOf(doc.documentTypeAr, doc.documentTypeEn) }}
                    </p>
                  </div>
                  <span class="text-xs tabular-nums text-fg-muted">{{ doc.expiryDate }}</span>
                </li>
              }
            </ul>
            <a
              routerLink="/documents/expiring"
              class="mt-3 inline-flex text-sm text-accent underline-offset-2 hover:underline"
            >
              {{ 'home.hr.viewExpiring' | transloco }}
            </a>
          </section>
        }
      </div>

      @if (showUiKit) {
        <div class="mt-8">
          <kadr-button variant="ghost" type="button" (click)="openUiKit()">
            {{ 'app.openUiKit' | transloco }}
          </kadr-button>
        </div>
      }
    }
  `,
})
export class HomePage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly homeApi = inject(HomeApi);
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);

  protected readonly showUiKit = isDevMode();
  protected readonly state = signal<HomeState>('loading');
  protected readonly dashboard = signal<HomeDashboardDto | null>(null);

  protected readonly welcomeTitle = computed(() => {
    const name = this.dashboard()?.greeting.fullName ?? this.auth.me()?.fullName ?? '';
    return this.transloco.translate('home.welcome', { name });
  });

  protected readonly welcomeSubtitle = computed(() => {
    const company = this.dashboard()?.greeting.tenantName ?? this.auth.me()?.tenant.name ?? '';
    return this.transloco.translate('home.welcomeCompany', { company });
  });

  ngOnInit(): void {
    void this.load();
  }

  reload(): void {
    void this.load();
  }

  openUiKit(): void {
    void this.router.navigateByUrl('/dev/ui-kit');
  }

  checklistLabel(key: string): string {
    const map: Record<string, string> = {
      logo: 'home.checklist.logo',
      invite: 'home.checklist.invite',
      employee: 'home.checklist.employee',
      shifts: 'home.checklist.shifts',
      template: 'home.checklist.template',
    };
    return this.transloco.translate(map[key] ?? 'home.checklist.template');
  }

  nameOf(ar: string, en: string | null | undefined): string {
    return this.transloco.getActiveLang() === 'en' ? en || ar : ar;
  }

  /** Test helper: force a specific view state. */
  setStateForTest(state: HomeState): void {
    this.state.set(state);
  }

  /** Test helper: inject a dashboard payload without calling the API. */
  setDashboardForTest(value: HomeDashboardDto | null): void {
    this.dashboard.set(value);
    this.state.set(value ? 'ready' : 'error');
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      await this.auth.restoreSession();
      const me = this.auth.me();
      if (!me) {
        throw new Error('no session');
      }
      const dash = await this.homeApi.getDashboard();
      this.dashboard.set(dash);
      this.title.setTitle(`${dash.greeting.tenantName} · Kadr`);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }
}
