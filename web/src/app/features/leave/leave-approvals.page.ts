import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LeaveApi, RequestInboxItem } from '../../core/api/leave.api';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-leave-approvals-page',
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
    <header class="mb-6 text-start">
      <h1 class="text-2xl font-bold text-fg">{{ 'leave.approvals.title' | transloco }}</h1>
      <p class="mt-1 text-sm text-fg-2">{{ 'leave.approvals.subtitle' | transloco }}</p>
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
        [title]="'leave.approvals.emptyTitle' | transloco"
        [description]="'leave.approvals.emptyBody' | transloco"
      />
    } @else {
      <ul class="flex flex-col gap-2">
        @for (item of items(); track item.taskId) {
          <li
            class="flex flex-col gap-3 rounded-lg border border-line bg-surface p-4 text-start shadow-sm sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="min-w-0">
              <p class="text-sm font-medium text-fg">
                {{ item.requesterNameAr }} · {{ item.requestType }}
              </p>
              <p class="mt-1 text-xs text-fg-muted">
                <bdi>{{ item.subjectId }}</bdi>
              </p>
            </div>
            @if (canApprove()) {
              <div class="flex flex-wrap gap-2">
                <kadr-button variant="primary" type="button" (click)="approve(item)">
                  {{ 'leave.approvals.approve' | transloco }}
                </kadr-button>
                <kadr-button variant="danger-soft" type="button" (click)="rejectTarget.set(item)">
                  {{ 'leave.approvals.reject' | transloco }}
                </kadr-button>
              </div>
            }
          </li>
        }
      </ul>
    }

    <kadr-drawer
      [open]="!!rejectTarget()"
      [title]="'leave.approvals.rejectTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="rejectTarget.set(null)"
    >
      <div class="flex flex-col gap-3 p-4 text-start">
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.approvals.rejectReason' | transloco }}</span>
          <textarea
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            rows="3"
            [(ngModel)]="rejectReason"
            name="reason"
          ></textarea>
        </label>
        <kadr-button variant="danger-soft" type="button" [loading]="saving()" (click)="reject()">
          {{ 'leave.approvals.reject' | transloco }}
        </kadr-button>
      </div>
    </kadr-drawer>
  `,
})
export class LeaveApprovalsPage implements OnInit {
  private readonly api = inject(LeaveApi);
  private readonly auth = inject(AuthService);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<RequestInboxItem[]>([]);
  protected readonly rejectTarget = signal<RequestInboxItem | null>(null);
  protected readonly saving = signal(false);
  protected readonly canApprove = computed(() =>
    this.auth.hasPermission(PermissionCodes.LeaveApprove),
  );
  protected rejectReason = '';

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('leave.approvals.title'));
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const page = await this.api.inbox();
      this.items.set(page.items);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected async approve(item: RequestInboxItem): Promise<void> {
    if (!this.canApprove()) {
      return;
    }
    try {
      await this.api.approve(item.taskId);
      this.toast.show(this.transloco.translate('leave.approvals.approved'), 'success');
      void this.reload();
    } catch (err) {
      const pd = problemDetailsFromError(err);
      this.toast.show(pd?.detail ?? this.transloco.translate('leave.approvals.failed'), 'danger');
    }
  }

  protected async reject(): Promise<void> {
    const item = this.rejectTarget();
    if (!this.canApprove() || !item || this.rejectReason.trim().length < 3) {
      return;
    }
    this.saving.set(true);
    try {
      await this.api.reject(item.taskId, this.rejectReason.trim());
      this.toast.show(this.transloco.translate('leave.approvals.rejected'), 'success');
      this.rejectTarget.set(null);
      this.rejectReason = '';
      void this.reload();
    } catch (err) {
      const pd = problemDetailsFromError(err);
      this.toast.show(pd?.detail ?? this.transloco.translate('leave.approvals.failed'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }
}
