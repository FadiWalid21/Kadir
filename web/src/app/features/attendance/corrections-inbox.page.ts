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
import { AttendanceApi, AttendanceCorrection, PagedCorrections } from '../../core/api/attendance.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-corrections-inbox-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    BadgeComponent,
    ButtonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
  ],
  template: `

    <header class="mb-4 flex flex-wrap items-start justify-between gap-3 text-start">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'attendance.corrections.inboxTitle' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'attendance.corrections.inboxSubtitle' | transloco }}</p>
      </div>
      @if (page(); as p) {
        <p class="text-sm tabular-nums text-fg-2">
          {{ 'attendance.corrections.pendingCount' | transloco: { count: p.pendingCount } }}
        </p>
      }
    </header>

    <div class="mb-4">
      <select
        class="rounded-md border border-line bg-surface px-3 py-2 text-sm text-fg"
        [(ngModel)]="statusFilter"
        (ngModelChange)="reload()"
      >
        <option value="Pending">{{ 'ui.statusPending' | transloco }}</option>
        <option value="">{{ 'attendance.requests.tabAll' | transloco }}</option>
        <option value="Approved">{{ 'ui.statusApproved' | transloco }}</option>
        <option value="Rejected">{{ 'ui.statusRejected' | transloco }}</option>
      </select>
    </div>

    @if (state() === 'loading') {
      <kadr-skeleton height="16rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'attendance.corrections.loadErrorTitle' | transloco"
        [description]="'attendance.corrections.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (page(); as p) {
      @if (p.items.length === 0) {
        <kadr-empty-state
          [title]="'attendance.corrections.emptyTitle' | transloco"
          [description]="'attendance.corrections.emptyBody' | transloco"
        />
      } @else {
        <ul class="flex flex-col gap-3">
          @for (item of p.items; track item.id) {
            <li class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
              <div class="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p class="text-sm font-medium text-fg">{{ displayName(item) }}</p>
                  <p class="mt-0.5 text-xs tabular-nums text-fg-muted">
                    {{ item.workDate }} ·
                    {{ ('attendance.corrections.types.' + item.problemType) | transloco }}
                  </p>
                </div>
                <kadr-badge [tone]="statusTone(item.status)" [label]="statusLabel(item.status)" />
              </div>
              <p class="mt-2 text-sm text-fg-2">{{ item.reason }}</p>
              <p class="mt-1 text-xs tabular-nums text-fg-muted">
                {{ 'attendance.corrections.proposedTime' | transloco }}:
                {{ item.proposedTime?.slice(0, 5) }}
              </p>

              @if (item.status === 'Pending' && canDecide()) {
                <div class="mt-3 flex flex-wrap gap-2">
                  <kadr-button
                    variant="primary"
                    type="button"
                    [loading]="busyId() === item.id"
                    (click)="approve(item)"
                  >
                    {{ 'attendance.corrections.approve' | transloco }}
                  </kadr-button>
                  <kadr-button
                    variant="danger-soft"
                    type="button"
                    [disabled]="busyId() === item.id"
                    (click)="openReject(item)"
                  >
                    {{ 'attendance.corrections.reject' | transloco }}
                  </kadr-button>
                </div>
              }
            </li>
          }
        </ul>
      }
    }

    @if (rejectTarget(); as target) {
      <div class="fixed inset-0 z-50 flex items-end justify-center sm:items-center" role="presentation">
        <button
          type="button"
          class="absolute inset-0 bg-fg/40"
          [attr.aria-label]="'ui.close' | transloco"
          (click)="rejectTarget.set(null)"
        ></button>
        <div
          class="relative z-10 w-full max-w-md rounded-t-lg border border-line bg-surface p-4 shadow-lg sm:rounded-lg"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="rejectTitleId"
        >
          <h2 [id]="rejectTitleId" class="text-lg font-bold text-fg text-start">
            {{ 'attendance.corrections.rejectTitle' | transloco }}
          </h2>
          <p class="mt-1 text-sm text-fg-2 text-start">
            {{ 'attendance.corrections.rejectBody' | transloco }}
          </p>
          <label class="mt-4 flex flex-col gap-1 text-sm text-start">
            <span class="text-fg-2">{{ 'attendance.corrections.rejectReason' | transloco }}</span>
            <textarea
              class="min-h-24 rounded-md border border-line bg-app px-3 py-2 text-fg"
              [(ngModel)]="rejectReason"
              name="rejectReason"
            ></textarea>
          </label>
          <div class="mt-4 flex justify-end gap-2">
            <kadr-button variant="ghost" type="button" (click)="rejectTarget.set(null)">
              {{ 'ui.cancel' | transloco }}
            </kadr-button>
            <kadr-button
              variant="danger-soft"
              type="button"
              [loading]="busyId() === target.id"
              (click)="confirmReject()"
            >
              {{ 'attendance.corrections.confirmReject' | transloco }}
            </kadr-button>
          </div>
        </div>
      </div>
    }
  `,
})
export class CorrectionsInboxPage implements OnInit {
  private readonly api = inject(AttendanceApi);
  private readonly auth = inject(AuthService);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly rejectTitleId = 'corr-reject-title';
  protected readonly state = signal<PageState>('loading');
  protected readonly page = signal<PagedCorrections | null>(null);
  protected readonly busyId = signal<string | null>(null);
  protected readonly rejectTarget = signal<AttendanceCorrection | null>(null);
  protected statusFilter = 'Pending';
  protected rejectReason = '';

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('attendance.corrections.inboxTitle'));
    void this.reload();
  }

  protected canDecide(): boolean {
    return this.auth.hasPermission(PermissionCodes.AttendanceEdit);
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const data = await this.api.listCorrectionsInbox(this.statusFilter || undefined);
      this.page.set(data);
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected displayName(item: AttendanceCorrection): string {
    return this.transloco.getActiveLang() === 'en'
      ? item.employeeNameEn || item.employeeNameAr
      : item.employeeNameAr || item.employeeNameEn;
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

  protected async approve(item: AttendanceCorrection): Promise<void> {
    this.busyId.set(item.id);
    try {
      await this.api.approveCorrection(item.id);
      this.toast.show(this.transloco.translate('attendance.corrections.approved'), 'success');
      await this.reload();
    } catch {
      this.toast.show(this.transloco.translate('attendance.corrections.decideFailed'), 'danger');
    } finally {
      this.busyId.set(null);
    }
  }

  protected openReject(item: AttendanceCorrection): void {
    this.rejectReason = '';
    this.rejectTarget.set(item);
  }

  protected async confirmReject(): Promise<void> {
    const target = this.rejectTarget();
    if (!target || this.rejectReason.trim().length < 3) {
      this.toast.show(this.transloco.translate('attendance.corrections.rejectRequired'), 'danger');
      return;
    }
    this.busyId.set(target.id);
    try {
      await this.api.rejectCorrection(target.id, this.rejectReason.trim());
      this.rejectTarget.set(null);
      this.toast.show(this.transloco.translate('attendance.corrections.rejected'), 'success');
      await this.reload();
    } catch {
      this.toast.show(this.transloco.translate('attendance.corrections.decideFailed'), 'danger');
    } finally {
      this.busyId.set(null);
    }
  }
}
