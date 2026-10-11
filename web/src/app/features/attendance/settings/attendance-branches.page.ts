import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AttendanceApi, BranchLocation } from '../../../core/api/attendance.api';
import { ButtonComponent } from '../../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../../shared/ui/skeleton/skeleton.component';
import { AttendanceSubnavComponent } from '../attendance-subnav.component';
import { AttendanceSettingsSubnavComponent } from './attendance-settings-subnav.component';
import { BranchLocationDrawerComponent } from './branch-location-drawer.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-attendance-branches-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    AttendanceSubnavComponent,
    AttendanceSettingsSubnavComponent,
    ButtonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
    BranchLocationDrawerComponent,
  ],
  template: `
    <kadr-attendance-subnav active="settings" />
    <kadr-attendance-settings-subnav active="branches" />

    <header class="mb-6 text-start">
      <h1 class="text-2xl font-bold text-fg">{{ 'attendance.settings.branchesTitle' | transloco }}</h1>
      <p class="mt-1 text-sm text-fg-2">{{ 'attendance.settings.branchesSubtitle' | transloco }}</p>
    </header>

    @if (state() === 'loading') {
      <kadr-skeleton height="12rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.settings.loadErrorTitle' | transloco"
        [description]="'attendance.settings.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (branches().length === 0) {
      <kadr-empty-state
        [title]="'attendance.settings.branchesEmptyTitle' | transloco"
        [description]="'attendance.settings.branchesEmptyBody' | transloco"
      />
    } @else {
      <ul class="flex flex-col gap-2">
        @for (b of branches(); track b.branchId) {
          <li
            class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-line bg-surface px-4 py-3 text-start shadow-sm"
          >
            <div>
              <p class="text-sm font-medium text-fg">{{ b.name }}</p>
              <p class="text-xs text-fg-muted">
                @if (b.isConfigured) {
                  {{ 'attendance.settings.configured' | transloco }} ·
                  {{ b.geofenceRadiusMeters }}m · {{ b.outOfRangePolicy }}
                } @else {
                  {{ 'attendance.settings.notConfigured' | transloco }}
                }
              </p>
            </div>
            <kadr-button variant="secondary" type="button" (click)="edit.set(b)">
              {{ 'attendance.settings.editLocation' | transloco }}
            </kadr-button>
          </li>
        }
      </ul>
    }

    <kadr-branch-location-drawer
      [branch]="edit()"
      (closed)="edit.set(null)"
      (saved)="onSaved($event)"
    />
  `,
})
export class AttendanceBranchesPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly branches = signal<BranchLocation[]>([]);
  protected readonly edit = signal<BranchLocation | null>(null);

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.settings.branchesTitle'));
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      this.branches.set(await this.api.listBranches());
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected onSaved(updated: BranchLocation): void {
    this.branches.update((list) =>
      list.map((b) => (b.branchId === updated.branchId ? updated : b)),
    );
    this.edit.set(null);
  }
}
