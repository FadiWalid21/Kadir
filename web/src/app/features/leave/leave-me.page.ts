import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LeaveApi, LeaveBalance, LeaveType } from '../../core/api/leave.api';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-leave-me-page',
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
    <header class="mb-6 flex flex-col gap-3 text-start sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'leave.me.title' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'leave.me.subtitle' | transloco }}</p>
      </div>
      <kadr-button variant="primary" type="button" (click)="drawerOpen.set(true)">
        {{ 'leave.me.request' | transloco }}
      </kadr-button>
    </header>

    @if (state() === 'loading') {
      <kadr-skeleton height="8rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'leave.loadErrorTitle' | transloco"
        [description]="'leave.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (balances().length === 0) {
      <kadr-empty-state
        [title]="'leave.me.emptyTitle' | transloco"
        [description]="'leave.me.emptyBody' | transloco"
      />
    } @else {
      <ul class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        @for (b of balances(); track b.leaveTypeId) {
          <li class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-sm font-medium text-fg">{{ typeName(b) }}</p>
            <p class="mt-2 text-2xl font-bold tabular-nums text-fg">{{ b.remaining }}</p>
            <p class="mt-1 text-xs text-fg-muted">
              {{ 'leave.me.used' | transloco }}: {{ b.used }} ·
              {{ 'leave.me.entitled' | transloco }}: {{ b.entitled }}
            </p>
          </li>
        }
      </ul>
    }

    <kadr-drawer
      [open]="drawerOpen()"
      [title]="'leave.me.requestTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="drawerOpen.set(false)"
    >
      <div class="flex flex-col gap-3 p-4 text-start">
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.me.type' | transloco }}</span>
          <select class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="leaveTypeId" name="type">
            @for (t of types(); track t.id) {
              <option [value]="t.id">{{ t.nameAr }}</option>
            }
          </select>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.me.start' | transloco }}</span>
          <input type="date" class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="startDate" name="start" />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.me.end' | transloco }}</span>
          <input type="date" class="rounded-md border border-line bg-surface px-3 py-2 text-fg" [(ngModel)]="endDate" name="end" />
        </label>
        <label class="flex items-center gap-2 text-sm text-fg">
          <input type="checkbox" [(ngModel)]="isHalfDay" name="half" />
          {{ 'leave.me.halfDay' | transloco }}
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.me.reason' | transloco }}</span>
          <textarea class="rounded-md border border-line bg-surface px-3 py-2 text-fg" rows="3" [(ngModel)]="reason" name="reason"></textarea>
        </label>
        <kadr-button variant="primary" type="button" [loading]="saving()" (click)="submit()">
          {{ 'leave.me.submit' | transloco }}
        </kadr-button>
      </div>
    </kadr-drawer>
  `,
})
export class LeaveMePage implements OnInit {
  private readonly api = inject(LeaveApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly balances = signal<LeaveBalance[]>([]);
  protected readonly types = signal<LeaveType[]>([]);
  protected readonly drawerOpen = signal(false);
  protected readonly saving = signal(false);

  protected leaveTypeId = '';
  protected startDate = '';
  protected endDate = '';
  protected isHalfDay = false;
  protected reason = '';

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('leave.me.title'));
    void this.reload();
  }

  protected typeName(b: LeaveBalance): string {
    return this.transloco.getActiveLang() === 'en' ? b.leaveTypeNameEn || b.leaveTypeNameAr : b.leaveTypeNameAr;
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const [balances, types] = await Promise.all([this.api.myBalances(), this.api.listActiveTypes()]);
      this.balances.set(balances);
      this.types.set(types);
      if (types[0]) {
        this.leaveTypeId = types[0].id;
      }
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected async submit(): Promise<void> {
    if (!this.leaveTypeId || !this.startDate || !this.endDate) {
      return;
    }
    this.saving.set(true);
    try {
      await this.api.createLeave({
        leaveTypeId: this.leaveTypeId,
        startDate: this.startDate,
        endDate: this.endDate,
        isHalfDay: this.isHalfDay,
        reason: this.reason.trim() || null,
      });
      this.toast.show(this.transloco.translate('leave.me.submitted'), 'success');
      this.drawerOpen.set(false);
      void this.reload();
    } catch (err) {
      const pd = problemDetailsFromError(err);
      this.toast.show(pd?.detail ?? this.transloco.translate('leave.me.submitFailed'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }
}
