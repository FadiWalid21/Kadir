import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PlatformApi } from '../../core/api/platform.api';
import { PlatformHealth } from '../../core/api/platform.models';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent, IconName } from '../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { DateFmtPipe } from './date-fmt.pipe';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-platform-health-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    ButtonComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DateFmtPipe,
    IconComponent,
  ],
  template: `
    <div class="flex flex-col gap-6">
      <header class="flex flex-wrap items-start justify-between gap-3">
        <div class="text-start">
          <h1 class="text-2xl font-bold text-fg">{{ 'platform.health.title' | transloco }}</h1>
          <p class="mt-1 text-sm text-fg-2">{{ 'platform.health.subtitle' | transloco }}</p>
        </div>
        <div class="flex flex-col items-end gap-1">
          <kadr-button
            variant="secondary"
            type="button"
            [loading]="state() === 'loading'"
            (click)="reload()"
          >
            <span class="inline-flex items-center gap-2">
              <kadr-icon name="refresh" size="sm" />
              {{ 'platform.health.refresh' | transloco }}
            </span>
          </kadr-button>
          @if (health(); as h) {
            <p class="text-xs text-fg-muted">
              {{ 'platform.health.checkedAt' | transloco }}: {{ h.checkedAt | dateFmt }}
            </p>
          }
        </div>
      </header>

      @if (state() === 'loading' && !health()) {
        <div class="grid gap-4 md:grid-cols-2">
          @for (_ of [1, 2, 3, 4]; track $index) {
            <kadr-skeleton height="8rem" />
          }
        </div>
      } @else if (state() === 'error') {
        <kadr-empty-state
          [title]="'platform.health.loadErrorTitle' | transloco"
          [description]="'platform.health.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">
            {{ 'home.retry' | transloco }}
          </kadr-button>
        </kadr-empty-state>
      } @else if (health(); as h) {
        <section
          class="rounded-lg border border-line px-4 py-3"
          [class.bg-success-soft]="bannerTone(h.status) === 'success'"
          [class.bg-warning-soft]="bannerTone(h.status) === 'warning'"
          [class.bg-danger-soft]="bannerTone(h.status) === 'danger'"
          [class.bg-subtle]="bannerTone(h.status) === 'neutral'"
        >
          <div class="flex flex-wrap items-center gap-2">
            <kadr-icon [name]="bannerIcon(h.status)" size="sm" />
            <kadr-badge [tone]="statusTone(h.status)" [label]="statusLabel(h.status)" />
            <p class="text-sm text-fg">{{ 'platform.health.overall' | transloco }}</p>
          </div>
        </section>

        <div class="grid gap-4 sm:grid-cols-2">
          @for (c of h.components; track c.name) {
            <article
              class="rounded-lg border border-line bg-surface p-5 text-start"
              [class.border-warning]="statusTone(c.status) === 'warning'"
              [class.border-danger]="statusTone(c.status) === 'danger'"
            >
              <div class="flex items-start justify-between gap-2">
                <div class="flex items-center gap-2">
                  <kadr-icon [name]="componentIcon(c.name)" size="sm" class="text-fg-muted" />
                  <h2 class="text-base font-bold text-fg">{{ componentLabel(c.name) }}</h2>
                </div>
                <kadr-badge [tone]="statusTone(c.status)" [label]="statusLabel(c.status)" />
              </div>
              @if (c.provider) {
                <p class="mt-2 text-sm text-fg-2">{{ c.provider }}</p>
              }
              @if (c.latencyMs !== null) {
                <p class="mt-1 text-sm tabular-nums text-fg-muted">
                  {{ 'platform.health.latency' | transloco: { ms: c.latencyMs } }}
                </p>
              }
              @if (c.detail) {
                <p class="mt-2 text-xs text-fg-2">{{ c.detail }}</p>
              }
            </article>
          }
        </div>

        <section class="rounded-lg border border-line bg-surface p-5">
          <h2 class="mb-3 text-base font-bold text-fg">{{ 'platform.health.versionInfo' | transloco }}</h2>
          <dl class="flex flex-col gap-2 text-sm">
            <div class="flex justify-between gap-3">
              <dt class="text-fg-2">{{ 'platform.health.checkedAt' | transloco }}</dt>
              <dd class="tabular-nums text-fg">{{ h.checkedAt | dateFmt }}</dd>
            </div>
            <div class="flex justify-between gap-3">
              <dt class="text-fg-2">{{ 'platform.health.serverTime' | transloco }}</dt>
              <dd class="tabular-nums text-fg">{{ h.checkedAt | dateFmt }}</dd>
            </div>
          </dl>
        </section>
      }
    </div>
  `,
})
export class PlatformHealthPage implements OnInit {
  private readonly api = inject(PlatformApi);
  private readonly i18n = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly health = signal<PlatformHealth | null>(null);

  ngOnInit(): void {
    void this.reload();
  }

  reload(): void {
    void this.load();
  }

  statusTone(status: string): BadgeTone {
    const s = status.toLowerCase().replace(/\s+/g, '_');
    if (s === 'healthy' || s === 'ok' || s === 'up' || s === 'configured') {
      return 'success';
    }
    if (s === 'degraded' || s === 'warning') {
      return 'warning';
    }
    if (s === 'unhealthy' || s === 'down' || s === 'error') {
      return 'danger';
    }
    if (s === 'not_configured' || s === 'notconfigured') {
      return 'neutral';
    }
    return 'neutral';
  }

  bannerTone(status: string): BadgeTone {
    return this.statusTone(status);
  }

  bannerIcon(status: string): IconName {
    const t = this.statusTone(status);
    if (t === 'success') {
      return 'check';
    }
    if (t === 'warning' || t === 'danger') {
      return 'alert';
    }
    return 'info';
  }

  statusLabel(status: string): string {
    const key = `platform.health.status.${status.toLowerCase()}`;
    const t = this.i18n.translate(key);
    return t === key ? status : t;
  }

  componentLabel(name: string): string {
    const key = `platform.health.component.${name.toLowerCase()}`;
    const t = this.i18n.translate(key);
    return t === key ? name : t;
  }

  componentIcon(name: string): IconName {
    switch (name.toLowerCase()) {
      case 'api':
        return 'link';
      case 'database':
        return 'file';
      case 'storage':
        return 'inbox';
      case 'email':
        return 'inbox';
      case 'webpush':
        return 'bell';
      default:
        return 'info';
    }
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      this.health.set(await this.api.health());
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }
}
