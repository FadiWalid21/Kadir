import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LeaveApi, LeaveType } from '../../core/api/leave.api';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-leave-types-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
    DrawerComponent,
  ],
  template: `
    <header class="mb-6 mt-6 text-start">
      <h1 class="text-2xl font-bold text-fg">{{ 'leave.settings.typesTitle' | transloco }}</h1>
      <p class="mt-1 text-sm text-fg-2">{{ 'leave.settings.typesSubtitle' | transloco }}</p>
    </header>

    @if (state() === 'loading') {
      <kadr-skeleton height="8rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'leave.loadErrorTitle' | transloco"
        [description]="'leave.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (items().length === 0) {
      <kadr-empty-state
        [title]="'leave.settings.typesEmptyTitle' | transloco"
        [description]="'leave.settings.typesEmptyBody' | transloco"
      />
    } @else {
      <ul class="flex flex-col gap-2">
        @for (t of items(); track t.id) {
          <li
            class="flex flex-col gap-2 rounded-lg border border-line bg-surface p-4 text-start sm:flex-row sm:items-center sm:justify-between"
          >
            <div>
              <p class="text-sm font-medium text-fg">{{ t.nameAr }}</p>
              <p class="text-xs text-fg-muted">
                {{ t.daysPerYear ?? '—' }} ·
                {{ t.isActive ? ('leave.settings.active' | transloco) : ('leave.settings.inactive' | transloco) }}
              </p>
            </div>
            <kadr-button variant="secondary" type="button" (click)="edit.set(t)">
              {{ 'attendance.settings.editLocation' | transloco }}
            </kadr-button>
          </li>
        }
      </ul>
    }

    <kadr-drawer
      [open]="!!edit()"
      [title]="'leave.settings.editType' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="edit.set(null)"
    >
      @if (edit(); as t) {
        <div class="flex flex-col gap-3 p-4 text-start">
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-fg-2">{{ 'attendance.locations.nameAr' | transloco }}</span>
            <input class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="nameAr" name="ar" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-fg-2">{{ 'attendance.locations.nameEn' | transloco }}</span>
            <input class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="nameEn" name="en" />
          </label>
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-fg-2">{{ 'leave.settings.daysPerYear' | transloco }}</span>
            <input type="number" class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="daysPerYear" name="days" />
          </label>
          <label class="flex items-center gap-2 text-sm">
            <input type="checkbox" [(ngModel)]="paid" name="paid" /> {{ 'leave.settings.paid' | transloco }}
          </label>
          <label class="flex items-center gap-2 text-sm">
            <input type="checkbox" [(ngModel)]="requiresApproval" name="appr" /> {{ 'leave.settings.requiresApproval' | transloco }}
          </label>
          <label class="flex items-center gap-2 text-sm">
            <input type="checkbox" [(ngModel)]="requiresAttachment" name="att" /> {{ 'leave.settings.requiresAttachment' | transloco }}
          </label>
          <label class="flex items-center gap-2 text-sm">
            <input type="checkbox" [(ngModel)]="isActive" name="active" /> {{ 'leave.settings.active' | transloco }}
          </label>
          <kadr-button variant="primary" type="button" [loading]="saving()" (click)="save(t.id)">
            {{ 'ui.save' | transloco }}
          </kadr-button>
        </div>
      }
    </kadr-drawer>
  `,
})
export class LeaveTypesSettingsPage implements OnInit {
  private readonly api = inject(LeaveApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<LeaveType[]>([]);
  protected readonly edit = signal<LeaveType | null>(null);
  protected readonly saving = signal(false);

  protected nameAr = '';
  protected nameEn = '';
  protected daysPerYear: number | null = null;
  protected paid = true;
  protected requiresApproval = true;
  protected requiresAttachment = false;
  protected isActive = true;

  constructor() {
    effect(() => {
      const t = this.edit();
      if (!t) {
        return;
      }
      this.nameAr = t.nameAr;
      this.nameEn = t.nameEn;
      this.daysPerYear = t.daysPerYear;
      this.paid = t.paid;
      this.requiresApproval = t.requiresApproval;
      this.requiresAttachment = t.requiresAttachment;
      this.isActive = t.isActive;
    });
  }

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('leave.settings.typesTitle'));
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      this.items.set(await this.api.listTypes());
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected async save(id: string): Promise<void> {
    this.saving.set(true);
    try {
      await this.api.updateType(id, {
        nameAr: this.nameAr,
        nameEn: this.nameEn,
        paid: this.paid,
        daysPerYear: this.daysPerYear,
        requiresApproval: this.requiresApproval,
        requiresAttachment: this.requiresAttachment,
        isActive: this.isActive,
      });
      this.toast.show(this.transloco.translate('attendance.settings.saved'), 'success');
      this.edit.set(null);
      void this.reload();
    } catch (err) {
      const pd = problemDetailsFromError(err);
      this.toast.show(pd?.detail ?? this.transloco.translate('attendance.settings.saveFailed'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }
}
