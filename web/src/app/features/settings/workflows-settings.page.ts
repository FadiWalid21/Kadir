import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LeaveApi, WorkflowDefinition } from '../../core/api/leave.api';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { NetworkErrorComponent } from '../../shared/ui/pwa/network-error.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-workflows-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    ButtonComponent,
    EmptyStateComponent,
    NetworkErrorComponent,
    SkeletonComponent,
  ],
  template: `
    <header class="mb-6 mt-6 flex flex-col gap-3 text-start sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold text-fg">{{ 'leave.settings.workflowsTitle' | transloco }}</h1>
        <p class="mt-1 text-sm text-fg-2">{{ 'leave.settings.workflowsSubtitle' | transloco }}</p>
      </div>
      <kadr-button variant="primary" type="button" [loading]="creating()" (click)="createDefault()">
        {{ 'leave.settings.addWorkflow' | transloco }}
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
    } @else if (items().length === 0) {
      <kadr-empty-state
        [title]="'leave.settings.workflowsEmptyTitle' | transloco"
        [description]="'leave.settings.workflowsEmptyBody' | transloco"
      />
    } @else {
      <ul class="flex flex-col gap-2">
        @for (w of items(); track w.id) {
          <li class="rounded-lg border border-line bg-surface p-4 text-start shadow-sm">
            <p class="text-sm font-medium text-fg">{{ w.nameAr }}</p>
            <p class="mt-1 text-xs text-fg-muted">
              {{ w.requestType }} · {{ w.steps.length }}
              {{ 'leave.settings.steps' | transloco }}
              @if (w.isDefault) {
                · {{ 'leave.settings.default' | transloco }}
              }
            </p>
          </li>
        }
      </ul>
    }
  `,
})
export class WorkflowsSettingsPage implements OnInit {
  private readonly api = inject(LeaveApi);
  private readonly title = inject(Title);
  private readonly transloco = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly items = signal<WorkflowDefinition[]>([]);
  protected readonly creating = signal(false);

  ngOnInit(): void {
    this.title.setTitle(this.transloco.translate('leave.settings.workflowsTitle'));
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.state.set('loading');
    try {
      this.items.set(await this.api.listWorkflows());
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }

  protected async createDefault(): Promise<void> {
    this.creating.set(true);
    try {
      await this.api.upsertWorkflow(null, {
        nameAr: 'اعتماد الإجازة',
        nameEn: 'Leave approval',
        requestType: 'Leave',
        isActive: true,
        isDefault: true,
        steps: [{ stepOrder: 1, approverKind: 'DirectManager', isFinal: true }],
      });
      this.toast.show(this.transloco.translate('attendance.settings.saved'), 'success');
      void this.reload();
    } catch {
      this.toast.show(this.transloco.translate('attendance.settings.saveFailed'), 'danger');
    } finally {
      this.creating.set(false);
    }
  }
}
