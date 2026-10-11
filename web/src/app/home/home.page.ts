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
import { AuthService } from '../core/auth/auth.service';
import { BadgeComponent } from '../shared/ui/badge/badge.component';
import { ButtonComponent } from '../shared/ui/button/button.component';
import { EmptyStateComponent } from '../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { InstallCardComponent } from '../shared/ui/pwa/install-card.component';
import { NetworkErrorComponent } from '../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../shared/ui/skeleton/skeleton.component';

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
    InstallCardComponent,
    NetworkErrorComponent,
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
    } @else {
      <kadr-install-card />

      <header class="mb-8 flex flex-col gap-2 text-start">
        <h1 class="text-2xl font-bold leading-tight text-fg md:text-3xl">
          {{ welcomeTitle() }}
        </h1>
        <p class="max-w-2xl text-sm text-fg-2">{{ welcomeSubtitle() }}</p>
      </header>

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
            {{ 'home.checklistProgress' | transloco: { done: checklistDone(), total: 4 } }}
          </p>
        </div>
        <ul class="flex flex-col gap-2">
          <li
            class="flex items-center justify-between gap-3 rounded-md border border-line bg-app px-4 py-3"
          >
            <a
              routerLink="/settings/company"
              class="text-sm text-accent underline-offset-2 hover:underline"
            >
              {{ 'home.checklist.logo' | transloco }}
            </a>
            @if (hasLogo()) {
              <span class="inline-flex items-center gap-1 text-sm text-success">
                <kadr-icon name="check" size="sm" />
                {{ 'home.checklistDone' | transloco }}
              </span>
            } @else {
              <kadr-badge tone="neutral" [label]="'home.checklistAction' | transloco" />
            }
          </li>
          <li
            class="flex items-center justify-between gap-3 rounded-md border border-line bg-app px-4 py-3"
          >
            <a
              routerLink="/settings/users"
              class="text-sm text-accent underline-offset-2 hover:underline"
            >
              {{ 'home.checklist.invite' | transloco }}
            </a>
            @if (inviteDone()) {
              <span class="inline-flex items-center gap-1 text-sm text-success">
                <kadr-icon name="check" size="sm" />
                {{ 'home.checklistDone' | transloco }}
              </span>
            } @else {
              <kadr-badge tone="neutral" [label]="'home.checklistAction' | transloco" />
            }
          </li>
          <li
            class="flex items-center justify-between gap-3 rounded-md border border-line bg-app px-4 py-3"
          >
            <a
              routerLink="/settings/templates"
              class="text-sm text-accent underline-offset-2 hover:underline"
            >
              {{ 'home.checklist.template' | transloco }}
            </a>
            <kadr-badge tone="neutral" [label]="'home.checklistAction' | transloco" />
          </li>
          <li
            class="flex items-center justify-between gap-3 rounded-md border border-line bg-app px-4 py-3"
          >
            <a
              routerLink="/employees"
              class="text-sm text-accent underline-offset-2 hover:underline"
            >
              {{ 'home.checklist.employee' | transloco }}
            </a>
            <kadr-badge tone="neutral" [label]="'home.checklistAction' | transloco" />
          </li>
        </ul>
      </section>

      <div class="grid gap-4 md:grid-cols-2">
        <section class="rounded-lg border border-line bg-surface p-5 shadow-sm">
          <h2 class="mb-3 text-base font-bold text-fg">
            {{ 'home.widgetApprovalsTitle' | transloco }}
          </h2>
          <kadr-empty-state
            [title]="'home.widgetEmptyTitle' | transloco"
            [description]="'home.widgetEmptyBody' | transloco"
          />
        </section>
        <section class="rounded-lg border border-line bg-surface p-5 shadow-sm">
          <h2 class="mb-3 text-base font-bold text-fg">
            {{ 'home.widgetAttendanceTitle' | transloco }}
          </h2>
          <kadr-empty-state
            [title]="'home.widgetEmptyTitle' | transloco"
            [description]="'home.widgetEmptyBody' | transloco"
          />
        </section>
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
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);

  protected readonly showUiKit = isDevMode();
  protected readonly state = signal<HomeState>('loading');

  protected readonly hasLogo = computed(() => !!this.auth.me()?.tenant.logoUrl);
  protected readonly inviteDone = computed(() => !!this.auth.me()?.checklist?.inviteTeamDone);

  protected readonly checklistDone = computed(
    () => (this.hasLogo() ? 1 : 0) + (this.inviteDone() ? 1 : 0),
  );

  protected readonly welcomeTitle = computed(() => {
    const name = this.auth.me()?.fullName ?? '';
    return this.transloco.translate('home.welcome', { name });
  });

  protected readonly welcomeSubtitle = computed(() => {
    const company = this.auth.me()?.tenant.name ?? '';
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

  /** Test helper: force a specific view state. */
  setStateForTest(state: HomeState): void {
    this.state.set(state);
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      await this.auth.restoreSession();
      const me = this.auth.me();
      if (!me) {
        throw new Error('no session');
      }
      this.title.setTitle(`${me.tenant.name} · Kadr`);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }
}
