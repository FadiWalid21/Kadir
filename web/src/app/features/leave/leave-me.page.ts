import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LeaveApi, LeaveBalance, LeaveType } from '../../core/api/leave.api';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';

type PageState = 'loading' | 'ready' | 'error' | 'noEmployee';

@Component({
  selector: 'kadr-leave-me-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
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
      @if (canRequest() && state() === 'ready') {
        <kadr-button variant="primary" type="button" (click)="openDrawer()">
          {{ 'leave.me.request' | transloco }}
        </kadr-button>
      }
    </header>

    @if (state() === 'loading') {
      <kadr-skeleton height="8rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'leave.loadErrorTitle' | transloco"
        [description]="'leave.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (state() === 'noEmployee') {
      <kadr-empty-state
        [title]="'leave.me.noEmployeeTitle' | transloco"
        [description]="'leave.me.noEmployeeBody' | transloco"
      >
        @if (canManageEmployees()) {
          <a routerLink="/employees" class="contents">
            <kadr-button variant="secondary" type="button">
              {{ 'attendance.card.setupLinkEmployees' | transloco }}
            </kadr-button>
          </a>
        }
      </kadr-empty-state>
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
          <select
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [ngModel]="leaveTypeId()"
            (ngModelChange)="onTypeChange($event)"
            name="type"
          >
            @for (t of types(); track t.id) {
              <option [value]="t.id">{{ typeLabel(t) }}</option>
            }
          </select>
        </label>
        @if (selectedRemaining() !== null) {
          <p class="text-xs text-fg-muted">
            {{ 'leave.me.remainingHint' | transloco: { days: selectedRemaining() } }}
          </p>
        }
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.me.start' | transloco }}</span>
          <input
            type="date"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="startDate"
            name="start"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.me.end' | transloco }}</span>
          <input
            type="date"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="endDate"
            name="end"
          />
        </label>
        <label class="flex items-center gap-2 text-sm text-fg">
          <input type="checkbox" [(ngModel)]="isHalfDay" name="half" />
          {{ 'leave.me.halfDay' | transloco }}
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.me.reason' | transloco }}</span>
          <textarea
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            rows="3"
            [(ngModel)]="reason"
            name="reason"
          ></textarea>
        </label>
        @if (selectedRequiresAttachment()) {
          <label class="flex flex-col gap-1 text-sm">
            <span class="text-fg-2">{{ 'leave.me.attachment' | transloco }} *</span>
            <span class="text-xs text-fg-muted">{{ 'leave.me.attachmentHint' | transloco }}</span>
            <input
              type="file"
              accept="image/jpeg,image/png"
              class="text-sm text-fg"
              (change)="onAttachment($event)"
            />
            @if (attachmentName()) {
              <span class="text-xs text-fg-muted">{{ attachmentName() }}</span>
            }
          </label>
        }
        <kadr-button variant="primary" type="button" [loading]="saving()" (click)="submit()">
          {{ 'leave.me.submit' | transloco }}
        </kadr-button>
      </div>
    </kadr-drawer>
  `,
})
export class LeaveMePage implements OnInit {
  private readonly api = inject(LeaveApi);
  private readonly auth = inject(AuthService);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly balances = signal<LeaveBalance[]>([]);
  protected readonly types = signal<LeaveType[]>([]);
  protected readonly drawerOpen = signal(false);
  protected readonly saving = signal(false);
  protected readonly leaveTypeId = signal('');
  protected readonly attachmentFile = signal<File | null>(null);
  protected readonly attachmentName = signal('');
  protected readonly canRequest = computed(() =>
    this.auth.hasPermission(PermissionCodes.LeaveRequest),
  );

  protected startDate = '';
  protected endDate = '';
  protected isHalfDay = false;
  protected reason = '';

  protected readonly selectedRequiresAttachment = computed(() => {
    const id = this.leaveTypeId();
    return this.types().find((t) => t.id === id)?.requiresAttachment === true;
  });

  protected readonly selectedRemaining = computed(() => {
    const id = this.leaveTypeId();
    const bal = this.balances().find((b) => b.leaveTypeId === id);
    return bal ? bal.remaining : null;
  });

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('leave.me.title'));
    void this.reload();
  }

  protected canManageEmployees(): boolean {
    return this.auth.hasPermission(PermissionCodes.EmployeesUpdate);
  }

  protected typeName(b: LeaveBalance): string {
    return this.transloco.getActiveLang() === 'en'
      ? b.leaveTypeNameEn || b.leaveTypeNameAr
      : b.leaveTypeNameAr;
  }

  protected typeLabel(t: LeaveType): string {
    return this.transloco.getActiveLang() === 'en' ? t.nameEn || t.nameAr : t.nameAr;
  }

  protected openDrawer(): void {
    this.attachmentFile.set(null);
    this.attachmentName.set('');
    this.drawerOpen.set(true);
  }

  protected onTypeChange(id: string): void {
    this.leaveTypeId.set(id);
    this.attachmentFile.set(null);
    this.attachmentName.set('');
  }

  protected onAttachment(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.attachmentFile.set(file);
    this.attachmentName.set(file?.name ?? '');
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const [balances, types] = await Promise.all([this.api.myBalances(), this.api.listActiveTypes()]);
      this.balances.set(balances);
      this.types.set(types);
      if (types[0]) {
        this.leaveTypeId.set(types[0].id);
      }
      this.state.set('ready');
    } catch (err) {
      if (problemDetailsFromError(err)?.code === 'attendance.no_employee') {
        this.balances.set([]);
        this.types.set([]);
        this.state.set('noEmployee');
        return;
      }
      this.state.set('error');
    }
  }

  protected async submit(): Promise<void> {
    if (!this.canRequest() || !this.leaveTypeId() || !this.startDate || !this.endDate) {
      return;
    }
    if (this.selectedRequiresAttachment() && !this.attachmentFile()) {
      this.toast.show(this.transloco.translate('leave.me.attachmentRequired'), 'danger');
      return;
    }
    this.saving.set(true);
    try {
      let attachmentFileId: string | null = null;
      const file = this.attachmentFile();
      if (file) {
        const uploaded = await this.api.uploadLeaveAttachment(file);
        attachmentFileId = uploaded.fileId;
      }
      await this.api.createLeave({
        leaveTypeId: this.leaveTypeId(),
        startDate: this.startDate,
        endDate: this.endDate,
        isHalfDay: this.isHalfDay,
        reason: this.reason.trim() || null,
        attachmentFileId,
      });
      this.toast.show(this.transloco.translate('leave.me.submitted'), 'success');
      this.drawerOpen.set(false);
      void this.reload();
    } catch (err) {
      const pd = problemDetailsFromError(err);
      const detail = pd?.detail?.trim();
      const genericAr = 'حدث خطأ.';
      const genericEn = 'An error occurred.';
      if (detail && detail !== genericAr && detail !== genericEn) {
        this.toast.show(detail, 'danger');
      } else if (pd?.code) {
        this.toast.show(
          `${this.transloco.translate('leave.me.submitFailed')} (${pd.code})`,
          'danger',
        );
      } else {
        this.toast.show(this.transloco.translate('leave.me.submitFailed'), 'danger');
      }
    } finally {
      this.saving.set(false);
    }
  }
}
