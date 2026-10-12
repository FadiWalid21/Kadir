import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AttendanceApi, WorkLocation } from '../../core/api/attendance.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { WorkLocationDrawerComponent } from './work-location-drawer.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-work-locations-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
    WorkLocationDrawerComponent,
  ],
  template: `

    <header class="mb-6 flex flex-col gap-3 text-start sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.locations.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.locations.subtitle' | transloco }}</p>
      </div>
      <kadr-button variant="primary" type="button" (click)="openCreate()">
        {{ 'attendance.locations.add' | transloco }}
      </kadr-button>
    </header>

    <div class="mb-4 flex flex-wrap gap-2">
      <input
        type="search"
        class="min-w-48 flex-1 rounded-md border border-line bg-surface px-3 py-2 text-sm text-fg"
        [placeholder]="'attendance.locations.search' | transloco"
        [(ngModel)]="q"
        name="q"
        (keyup.enter)="reload()"
      />
      <select
        class="rounded-md border border-line bg-surface px-3 py-2 text-sm text-fg"
        [(ngModel)]="typeFilter"
        name="type"
        (ngModelChange)="reload()"
      >
        <option value="">{{ 'attendance.locations.filterAllTypes' | transloco }}</option>
        <option value="Branch">{{ 'attendance.locations.typeBranch' | transloco }}</option>
        <option value="ExternalSite">{{ 'attendance.locations.typeExternal' | transloco }}</option>
      </select>
      <select
        class="rounded-md border border-line bg-surface px-3 py-2 text-sm text-fg"
        [(ngModel)]="activeFilter"
        name="active"
        (ngModelChange)="reload()"
      >
        <option value="all">{{ 'attendance.locations.filterAllStatus' | transloco }}</option>
        <option value="true">{{ 'attendance.locations.active' | transloco }}</option>
        <option value="false">{{ 'attendance.locations.inactive' | transloco }}</option>
      </select>
      <kadr-button variant="secondary" type="button" (click)="reload()">
        {{ 'attendance.today.applySearch' | transloco }}
      </kadr-button>
    </div>

    @if (state() === 'loading') {
      <div class="flex flex-col gap-2">
        <kadr-skeleton height="4.5rem" />
        <kadr-skeleton height="4.5rem" />
      </div>
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.locations.loadErrorTitle' | transloco"
        [description]="'attendance.locations.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (items().length === 0) {
      <kadr-empty-state
        [title]="'attendance.locations.emptyTitle' | transloco"
        [description]="'attendance.locations.emptyBody' | transloco"
      />
    } @else {
      <ul class="grid gap-3 md:grid-cols-2">
        @for (loc of items(); track loc.id) {
          <li
            class="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4 text-start shadow-sm"
          >
            <div class="flex items-start justify-between gap-2">
              <div class="min-w-0">
                <p class="truncate text-sm font-medium text-fg">
                  {{ displayName(loc) }}
                </p>
                <p class="mt-1 text-xs text-fg-muted">
                  {{
                    loc.type === 'Branch'
                      ? ('attendance.locations.typeBranch' | transloco)
                      : ('attendance.locations.typeExternal' | transloco)
                  }}
                  ·
                  {{
                    loc.isActive
                      ? ('attendance.locations.active' | transloco)
                      : ('attendance.locations.inactive' | transloco)
                  }}
                  · {{ loc.geofenceRadiusMeters }}m
                </p>
                @if (loc.address) {
                  <p class="mt-1 truncate text-xs text-fg-2">{{ loc.address }}</p>
                }
                <p class="mt-1 text-xs text-fg-muted tabular-nums" dir="ltr">
                  {{ loc.latitude }}, {{ loc.longitude }}
                </p>
              </div>
            </div>
            <div class="flex flex-wrap gap-2">
              <kadr-button variant="secondary" type="button" (click)="openEdit(loc)">
                {{ 'attendance.settings.editLocation' | transloco }}
              </kadr-button>
              <a
                [routerLink]="['/attendance/locations', loc.id, 'assign']"
                class="inline-flex min-h-11 items-center rounded-md border border-line bg-surface px-4 text-sm text-fg hover:bg-hover"
              >
                {{ 'attendance.locations.assign' | transloco }}
              </a>
            </div>
          </li>
        }
      </ul>
    }

    <kadr-work-location-drawer
      [location]="edit()"
      [creating]="creating()"
      (closed)="closeDrawer()"
      (saved)="onSaved()"
    />
  `,
})
export class WorkLocationsListPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<WorkLocation[]>([]);
  protected readonly edit = signal<WorkLocation | null>(null);
  protected readonly creating = signal(false);

  protected q = '';
  protected typeFilter = '';
  protected activeFilter: 'all' | 'true' | 'false' = 'all';

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.locations.title'));
    void this.reload();
  }

  protected displayName(loc: WorkLocation): string {
    const lang = this.transloco.getActiveLang();
    if (lang === 'en' && loc.nameEn) {
      return loc.nameEn;
    }
    return loc.nameAr;
  }

  protected openCreate(): void {
    this.edit.set(null);
    this.creating.set(true);
  }

  protected openEdit(loc: WorkLocation): void {
    this.creating.set(false);
    this.edit.set(loc);
  }

  protected closeDrawer(): void {
    this.creating.set(false);
    this.edit.set(null);
  }

  protected onSaved(): void {
    this.closeDrawer();
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const page = await this.api.listWorkLocations({
        q: this.q.trim() || undefined,
        type: this.typeFilter || undefined,
        active: this.activeFilter === 'all' ? undefined : this.activeFilter === 'true',
        page: 1,
        pageSize: 50,
      });
      this.items.set(page.items);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }
}
