import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  OnChanges,
  signal,
  SimpleChanges,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import {
  EmployeeInsuranceApi,
  EmployeeInsuranceProfile,
} from '../../core/api/employee-insurance.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type SectionState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'kadr-employee-insurance-section',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DrawerComponent,
  ],
  template: `
    <section class="flex flex-col gap-3 text-start">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h3 class="text-sm font-bold text-fg">{{ 'employees.insurance.title' | transloco }}</h3>
          <p class="mt-1 text-xs text-fg-muted">{{ 'employees.insurance.subtitle' | transloco }}</p>
        </div>
        @if (canMutate()) {
          <kadr-button variant="secondary" type="button" (click)="openEdit()">
            {{
              (profile() ? 'employees.insurance.edit' : 'employees.insurance.add') | transloco
            }}
          </kadr-button>
        }
      </div>

      @if (state() === 'loading') {
        <kadr-skeleton height="6rem" />
      } @else if (state() === 'error') {
        <kadr-empty-state
          [title]="'employees.insurance.loadErrorTitle' | transloco"
          [description]="'employees.insurance.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">
            {{ 'home.retry' | transloco }}
          </kadr-button>
        </kadr-empty-state>
      } @else if (!profile()) {
        <p class="text-sm text-fg-2">{{ 'employees.insurance.empty' | transloco }}</p>
      } @else {
        <div class="rounded-lg border border-line bg-surface p-4 text-sm">
          <p class="font-medium text-fg">
            {{
              (profile()!.isInsured
                ? 'employees.insurance.insured'
                : 'employees.insurance.notInsured'
              ) | transloco
            }}
          </p>
          @if (profile()!.isInsured) {
            <p class="mt-2 text-fg-2">
              {{ 'employees.insurance.number' | transloco }}:
              <span class="font-mono">{{ profile()!.insuranceNumber }}</span>
            </p>
            <p class="mt-1 text-fg-2 tabular-nums">
              {{ 'employees.insurance.startDate' | transloco }}:
              {{ profile()!.insuranceStartDate }}
            </p>
            @if (profile()!.insurableWageOverride != null) {
              <p class="mt-1 text-fg-2 tabular-nums">
                {{ 'employees.insurance.wageOverride' | transloco }}:
                {{ profile()!.insurableWageOverride }}
              </p>
            }
          }
        </div>
      }
    </section>

    <kadr-drawer
      [open]="drawerOpen()"
      [title]="'employees.insurance.editTitle' | transloco"
      [closeLabel]="'ui.cancel' | transloco"
      [hasFooter]="false"
      (closed)="closeDrawer()"
    >
      <form class="flex flex-col gap-4" (ngSubmit)="onSave()">
        <label class="flex items-center gap-2 text-sm text-fg">
          <input
            type="checkbox"
            class="size-4 rounded border-line"
            [ngModel]="draftInsured()"
            (ngModelChange)="draftInsured.set($event)"
            name="insured"
          />
          {{ 'employees.insurance.isInsured' | transloco }}
        </label>
        @if (draftInsured()) {
          <kadr-input
            [label]="'employees.insurance.number' | transloco"
            [value]="draftNumber()"
            (valueChange)="draftNumber.set($event)"
          />
          <kadr-input
            [label]="'employees.insurance.startDate' | transloco"
            type="date"
            [value]="draftStart()"
            (valueChange)="draftStart.set($event)"
          />
          <kadr-input
            [label]="'employees.insurance.wageOverride' | transloco"
            type="number"
            [value]="draftOverride()"
            (valueChange)="draftOverride.set($event)"
            [hint]="'employees.insurance.wageOverrideHint' | transloco"
          />
        }
        <div class="mt-2 flex flex-wrap justify-end gap-2">
          <kadr-button variant="secondary" type="button" (click)="closeDrawer()">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
          <kadr-button variant="primary" type="submit" [loading]="busy()" [disabled]="!canMutate()">
            {{ 'ui.save' | transloco }}
          </kadr-button>
        </div>
      </form>
    </kadr-drawer>
  `,
})
export class EmployeeInsuranceSectionComponent implements OnChanges {
  private readonly api = inject(EmployeeInsuranceApi);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(TranslocoService);

  readonly employeeId = input.required<string>();

  protected readonly state = signal<SectionState>('loading');
  protected readonly profile = signal<EmployeeInsuranceProfile | null>(null);
  protected readonly busy = signal(false);
  protected readonly drawerOpen = signal(false);
  protected readonly draftInsured = signal(false);
  protected readonly draftNumber = signal('');
  protected readonly draftStart = signal('');
  protected readonly draftOverride = signal('');

  protected readonly canMutate = computed(
    () =>
      this.auth.hasPermission(PermissionCodes.SalaryManage) && !this.subscription.readOnly(),
  );

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['employeeId']) {
      void this.reload();
    }
  }

  reload(): void {
    void this.load();
  }

  openEdit(): void {
    const p = this.profile();
    this.draftInsured.set(p?.isInsured ?? false);
    this.draftNumber.set(p?.insuranceNumber ?? '');
    this.draftStart.set(p?.insuranceStartDate ?? '');
    this.draftOverride.set(
      p?.insurableWageOverride != null ? String(p.insurableWageOverride) : '',
    );
    this.drawerOpen.set(true);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  async onSave(): Promise<void> {
    const insured = this.draftInsured();
    if (insured && (!this.draftNumber().trim() || !this.draftStart().trim())) {
      this.toast.show(this.i18n.translate('employees.insurance.toastValidation'), 'danger');
      return;
    }

    let override: number | null = null;
    const rawOverride = this.draftOverride().trim();
    if (rawOverride) {
      override = Number(rawOverride);
      if (Number.isNaN(override) || override < 0) {
        this.toast.show(this.i18n.translate('employees.insurance.toastValidation'), 'danger');
        return;
      }
    }

    this.busy.set(true);
    try {
      const saved = await this.api.upsert(this.employeeId(), {
        isInsured: insured,
        insuranceNumber: insured ? this.draftNumber().trim() : null,
        insuranceStartDate: insured ? this.draftStart().trim() : null,
        insurableWageOverride: insured ? override : null,
      });
      this.profile.set(saved);
      this.closeDrawer();
      this.toast.show(this.i18n.translate('employees.insurance.toastSaved'), 'success');
    } catch (err) {
      const code = problemDetailsFromError(err)?.code;
      const key =
        code === 'insurance.validation_failed'
          ? 'employees.insurance.toastValidation'
          : 'employees.insurance.toastFail';
      this.toast.show(this.i18n.translate(key), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    const id = this.employeeId();
    if (!id) {
      return;
    }
    this.state.set('loading');
    try {
      this.profile.set(await this.api.get(id));
      this.state.set('ready');
    } catch {
      this.state.set('error');
    }
  }
}
