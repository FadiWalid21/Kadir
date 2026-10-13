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
import { LeaveApi, LeaveBalance } from '../../core/api/leave.api';
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
  selector: 'kadr-leave-balances-page',
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
      <h1 class="text-2xl font-bold text-fg">{{ 'leave.balances.title' | transloco }}</h1>
      <p class="mt-1 text-sm text-fg-2">{{ 'leave.balances.subtitle' | transloco }}</p>
    </header>

    @if (state() === 'loading') {
      <kadr-skeleton height="10rem" />
    } @else if (state() === 'error') {
      <kadr-network-error
        [title]="'leave.loadErrorTitle' | transloco"
        [description]="'leave.loadErrorBody' | transloco"
        (retry)="reload()"
      />
    } @else if (items().length === 0) {
      <kadr-empty-state
        [title]="'leave.balances.emptyTitle' | transloco"
        [description]="'leave.balances.emptyBody' | transloco"
      />
    } @else {
      <div class="overflow-x-auto rounded-lg border border-line bg-surface shadow-sm">
        <table class="w-full min-w-0 text-start text-sm">
          <thead class="border-b border-line bg-app text-fg-2">
            <tr>
              <th class="px-4 py-3 font-medium">{{ 'leave.balances.colEmployee' | transloco }}</th>
              <th class="px-4 py-3 font-medium">{{ 'leave.balances.colType' | transloco }}</th>
              <th class="px-4 py-3 font-medium">{{ 'leave.balances.colRemaining' | transloco }}</th>
              @if (canAdjust()) {
                <th class="px-4 py-3 font-medium">{{ 'leave.balances.colActions' | transloco }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of items(); track row.employeeId + row.leaveTypeId) {
              <tr class="border-b border-line last:border-0">
                <td class="px-4 py-3 text-fg">{{ row.employeeNameAr }}</td>
                <td class="px-4 py-3 text-fg-2">{{ row.leaveTypeNameAr }}</td>
                <td class="px-4 py-3 tabular-nums text-fg">{{ row.remaining }}</td>
                @if (canAdjust()) {
                  <td class="px-4 py-3">
                    <kadr-button variant="secondary" type="button" (click)="openAdjust(row)">
                      {{ 'leave.balances.adjust' | transloco }}
                    </kadr-button>
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <kadr-drawer
      [open]="!!adjustRow()"
      [title]="'leave.balances.adjustTitle' | transloco"
      [closeLabel]="'ui.close' | transloco"
      (closed)="adjustRow.set(null)"
    >
      <div class="flex flex-col gap-3 p-4 text-start">
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.balances.delta' | transloco }}</span>
          <input
            type="number"
            step="0.5"
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            [(ngModel)]="delta"
            name="delta"
          />
        </label>
        <label class="flex flex-col gap-1 text-sm">
          <span class="text-fg-2">{{ 'leave.balances.reason' | transloco }}</span>
          <textarea
            class="rounded-md border border-line bg-surface px-3 py-2 text-fg"
            rows="3"
            [(ngModel)]="reason"
            name="reason"
          ></textarea>
        </label>
        <kadr-button variant="primary" type="button" [loading]="saving()" (click)="saveAdjust()">
          {{ 'ui.save' | transloco }}
        </kadr-button>
      </div>
    </kadr-drawer>
  `,
})
export class LeaveBalancesPage implements OnInit {
  private readonly api = inject(LeaveApi);
  private readonly auth = inject(AuthService);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<LeaveBalance[]>([]);
  protected readonly adjustRow = signal<LeaveBalance | null>(null);
  protected readonly saving = signal(false);
  protected readonly canAdjust = computed(() =>
    this.auth.hasPermission(PermissionCodes.LeaveManage),
  );
  protected delta = 1;
  protected reason = '';

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('leave.balances.title'));
    void this.reload();
  }

  protected openAdjust(row: LeaveBalance): void {
    if (!this.canAdjust()) {
      return;
    }
    this.delta = 1;
    this.reason = '';
    this.adjustRow.set(row);
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      this.items.set(await this.api.listBalances());
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected async saveAdjust(): Promise<void> {
    const row = this.adjustRow();
    if (!this.canAdjust() || !row || !this.reason.trim() || this.delta === 0) {
      return;
    }
    this.saving.set(true);
    try {
      await this.api.adjustBalance({
        employeeId: row.employeeId,
        leaveTypeId: row.leaveTypeId,
        year: row.year,
        deltaDays: this.delta,
        reason: this.reason.trim(),
      });
      this.toast.show(this.transloco.translate('attendance.settings.saved'), 'success');
      this.adjustRow.set(null);
      void this.reload();
    } catch (err) {
      const pd = problemDetailsFromError(err);
      this.toast.show(pd?.detail ?? this.transloco.translate('attendance.settings.saveFailed'), 'danger');
    } finally {
      this.saving.set(false);
    }
  }
}
