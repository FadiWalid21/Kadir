import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  CreateEmployeeBody,
  EmergencyContact,
  EmployeesApi,
  OrgLookups,
} from '../../core/api/employees.api';
import { DirtyFormRegistry } from '../../core/forms/dirty-form.registry';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { SelectComponent, SelectOption } from '../../shared/ui/select/select.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

type PageState = 'loading' | 'ready' | 'error' | 'locked' | 'cap';

const DIRTY_ID = 'employee-form';

@Component({
  selector: 'kadr-employee-form-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    SelectComponent,
    SkeletonComponent,
    EmptyStateComponent,
    ConfirmDialogComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-3xl flex-col gap-5 pb-24">
      <nav class="text-sm text-fg-2">
        <a routerLink="/employees" class="text-accent hover:underline">{{
          'employees.title' | transloco
        }}</a>
        <span class="mx-1">/</span>
        <span>{{ (isEdit() ? 'employees.editTitle' : 'employees.addTitle') | transloco }}</span>
      </nav>

      <header class="text-start">
        <h1 class="text-2xl font-bold text-fg">
          {{ (isEdit() ? 'employees.editTitle' : 'employees.addTitle') | transloco }}
        </h1>
      </header>

      @if (state() === 'loading') {
        <kadr-skeleton height="8rem" />
        <kadr-skeleton height="8rem" />
      } @else if (state() === 'locked') {
        <kadr-empty-state
          [title]="'employees.featureLockedTitle' | transloco"
          [description]="'employees.featureLockedBody' | transloco"
        >
          <a routerLink="/settings/subscription" class="contents">
            <kadr-button variant="primary" type="button">{{
              'employees.viewPlans' | transloco
            }}</kadr-button>
          </a>
        </kadr-empty-state>
      } @else if (state() === 'error') {
        <kadr-empty-state
          [title]="'employees.loadErrorTitle' | transloco"
          [description]="'employees.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">{{
            'home.retry' | transloco
          }}</kadr-button>
        </kadr-empty-state>
      } @else {
        <section class="rounded-lg border border-line bg-surface p-4">
          <button
            type="button"
            class="flex w-full items-center justify-between text-start md:pointer-events-none"
            (click)="toggle('personal')"
          >
            <h2 class="text-base font-bold text-fg">{{ 'employees.sectionPersonal' | transloco }}</h2>
            <span class="text-fg-2 md:hidden">{{ open().personal ? '−' : '+' }}</span>
          </button>
          @if (open().personal) {
            <div class="mt-4 grid gap-3 sm:grid-cols-2">
              <kadr-input
                [label]="'employees.fullNameAr' | transloco"
                [required]="true"
                [value]="fullNameAr()"
                (valueChange)="setField('fullNameAr', $event)"
              />
              <kadr-input
                [label]="'employees.fullNameEn' | transloco"
                [value]="fullNameEn()"
                (valueChange)="setField('fullNameEn', $event)"
              />
              <kadr-input
                [label]="'employees.nationalId' | transloco"
                [value]="nationalId()"
                (valueChange)="setField('nationalId', $event)"
              />
              <kadr-select
                [label]="'employees.gender' | transloco"
                [options]="genderOptions()"
                [value]="gender()"
                (valueChange)="setField('gender', $event)"
              />
              <label class="grid gap-2 text-sm font-medium text-fg">
                <span>{{ 'employees.dateOfBirth' | transloco }}</span>
                <input
                  type="date"
                  class="h-control rounded-md border border-line-strong bg-surface px-3 text-base"
                  [ngModel]="dateOfBirth()"
                  (ngModelChange)="setField('dateOfBirth', $event)"
                />
              </label>
            </div>
          }
        </section>

        <section class="rounded-lg border border-line bg-surface p-4">
          <button
            type="button"
            class="flex w-full items-center justify-between text-start md:pointer-events-none"
            (click)="toggle('contact')"
          >
            <h2 class="text-base font-bold text-fg">{{ 'employees.sectionContact' | transloco }}</h2>
            <span class="text-fg-2 md:hidden">{{ open().contact ? '−' : '+' }}</span>
          </button>
          @if (open().contact) {
            <div class="mt-4 grid gap-3 sm:grid-cols-2">
              <kadr-input
                [label]="'ui.email' | transloco"
                [value]="email()"
                (valueChange)="setField('email', $event)"
              />
              <kadr-input
                [label]="'employees.phone' | transloco"
                [value]="phone()"
                (valueChange)="setField('phone', $event)"
              />
              <div class="sm:col-span-2">
                <kadr-input
                  [label]="'employees.address' | transloco"
                  [value]="address()"
                  (valueChange)="setField('address', $event)"
                />
              </div>
            </div>
          }
        </section>

        <section class="rounded-lg border border-line bg-surface p-4">
          <button
            type="button"
            class="flex w-full items-center justify-between text-start md:pointer-events-none"
            (click)="toggle('emergency')"
          >
            <h2 class="text-base font-bold text-fg">{{ 'employees.sectionEmergency' | transloco }}</h2>
            <span class="text-fg-2 md:hidden">{{ open().emergency ? '−' : '+' }}</span>
          </button>
          @if (open().emergency) {
            <div class="mt-4 flex flex-col gap-4">
              @for (c of emergency(); track $index; let i = $index) {
                <div class="grid gap-3 rounded-md border border-line p-3 sm:grid-cols-3">
                  <kadr-input
                    [label]="'employees.emergencyName' | transloco"
                    [value]="c.name"
                    (valueChange)="patchEmergency(i, 'name', $event)"
                  />
                  <kadr-input
                    [label]="'employees.emergencyRelation' | transloco"
                    [value]="c.relationship"
                    (valueChange)="patchEmergency(i, 'relationship', $event)"
                  />
                  <kadr-input
                    [label]="'employees.emergencyPhone' | transloco"
                    [value]="c.phone"
                    (valueChange)="patchEmergency(i, 'phone', $event)"
                  />
                </div>
              }
              <kadr-button variant="secondary" type="button" (click)="addEmergency()">{{
                'employees.addEmergency' | transloco
              }}</kadr-button>
            </div>
          }
        </section>

        <section class="rounded-lg border border-line bg-surface p-4">
          <button
            type="button"
            class="flex w-full items-center justify-between text-start md:pointer-events-none"
            (click)="toggle('job')"
          >
            <h2 class="text-base font-bold text-fg">{{ 'employees.sectionJob' | transloco }}</h2>
            <span class="text-fg-2 md:hidden">{{ open().job ? '−' : '+' }}</span>
          </button>
          @if (open().job) {
            <div class="mt-4 grid gap-3 sm:grid-cols-2">
              <kadr-select
                [label]="'glossary.branch' | transloco"
                [options]="branchOptions()"
                [value]="branchId()"
                (valueChange)="setField('branchId', $event)"
              />
              <kadr-select
                [label]="'glossary.department' | transloco"
                [options]="departmentOptions()"
                [value]="departmentId()"
                (valueChange)="setField('departmentId', $event)"
              />
              <kadr-select
                [label]="'glossary.jobTitle' | transloco"
                [options]="jobOptions()"
                [value]="jobTitleId()"
                (valueChange)="setField('jobTitleId', $event)"
              />
              <kadr-select
                [label]="'employees.manager' | transloco"
                [options]="managerOptions()"
                [value]="managerEmployeeId()"
                (valueChange)="setField('managerEmployeeId', $event)"
              />
              <label class="grid gap-2 text-sm font-medium text-fg">
                <span>{{ 'employees.hireDate' | transloco }} *</span>
                <input
                  type="date"
                  class="h-control rounded-md border border-line-strong bg-surface px-3 text-base"
                  [ngModel]="hireDate()"
                  (ngModelChange)="setField('hireDate', $event)"
                />
              </label>
              <label class="grid gap-2 text-sm font-medium text-fg">
                <span>{{ 'employees.probationEnd' | transloco }}</span>
                <input
                  type="date"
                  class="h-control rounded-md border border-line-strong bg-surface px-3 text-base"
                  [ngModel]="probationEndDate()"
                  (ngModelChange)="setField('probationEndDate', $event)"
                />
              </label>
              <kadr-select
                [label]="'employees.contractType' | transloco"
                [options]="contractOptions()"
                [value]="contractType()"
                (valueChange)="setField('contractType', $event)"
              />
              @if (!isEdit()) {
                <kadr-select
                  [label]="'employees.colStatus' | transloco"
                  [options]="statusOptions()"
                  [value]="status()"
                  (valueChange)="setField('status', $event)"
                />
              }
              @if (employeeNumber()) {
                <kadr-input
                  [label]="'employees.colNumber' | transloco"
                  [value]="employeeNumber()"
                  [readOnly]="true"
                />
              }
            </div>
          }
        </section>

        @if (formError()) {
          <p class="text-sm text-danger" role="alert">{{ formError() }}</p>
        }

        <div
          class="fixed inset-x-0 bottom-0 z-20 border-t border-line bg-surface px-4 py-3"
        >
          <div class="mx-auto flex max-w-3xl flex-wrap justify-end gap-2">
            <a routerLink="/employees" class="contents">
              <kadr-button variant="secondary" type="button">{{ 'ui.cancel' | transloco }}</kadr-button>
            </a>
            <kadr-button
              variant="primary"
              type="button"
              [loading]="saving()"
              (click)="save()"
            >
              {{ 'ui.save' | transloco }}
            </kadr-button>
          </div>
        </div>
      }

      <kadr-confirm-dialog
        [open]="showCapDialog()"
        [title]="'employees.capDialogTitle' | transloco"
        [message]="'employees.capDialogBody' | transloco"
        [confirmLabel]="'employees.viewPlans' | transloco"
        [cancelLabel]="'ui.close' | transloco"
        (confirmed)="goPlans()"
        (cancelled)="showCapDialog.set(false)"
      />
      <kadr-confirm-dialog
        [open]="showLeaveDialog()"
        [title]="'employees.unsavedTitle' | transloco"
        [message]="'employees.unsavedBody' | transloco"
        [confirmLabel]="'employees.leaveAnyway' | transloco"
        [cancelLabel]="'ui.cancel' | transloco"
        [danger]="true"
        (confirmed)="confirmLeave()"
        (cancelled)="showLeaveDialog.set(false)"
      />
    </div>
  `,
})
export class EmployeeFormPage implements OnInit, OnDestroy {
  private readonly api = inject(EmployeesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslocoService);
  private readonly toast = inject(ToastService);
  private readonly dirty = inject(DirtyFormRegistry);

  protected readonly state = signal<PageState>('loading');
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly showCapDialog = signal(false);
  protected readonly showLeaveDialog = signal(false);
  protected readonly isEdit = signal(false);
  protected readonly employeeId = signal<string | null>(null);
  protected readonly lookups = signal<OrgLookups | null>(null);
  protected readonly managerOptions = signal<SelectOption[]>([
    { value: '', label: '—' },
  ]);

  protected readonly open = signal({
    personal: true,
    contact: true,
    emergency: true,
    job: true,
  });

  protected readonly fullNameAr = signal('');
  protected readonly fullNameEn = signal('');
  protected readonly nationalId = signal('');
  protected readonly gender = signal('');
  protected readonly dateOfBirth = signal('');
  protected readonly email = signal('');
  protected readonly phone = signal('');
  protected readonly address = signal('');
  protected readonly branchId = signal('');
  protected readonly departmentId = signal('');
  protected readonly jobTitleId = signal('');
  protected readonly managerEmployeeId = signal('');
  protected readonly hireDate = signal(new Date().toISOString().slice(0, 10));
  protected readonly probationEndDate = signal('');
  protected readonly contractType = signal('Unlimited');
  protected readonly status = signal('Active');
  protected readonly employeeNumber = signal('');
  protected readonly emergency = signal<EmergencyContact[]>([
    { name: '', relationship: '', phone: '' },
  ]);

  private leaveUrl: string | null = null;

  protected readonly branchOptions = computed((): SelectOption[] =>
    (this.lookups()?.branches ?? []).map((b) => ({ value: b.id, label: b.nameAr })),
  );
  protected readonly departmentOptions = computed((): SelectOption[] => {
    const lang = this.i18n.getActiveLang();
    return [
      { value: '', label: '—' },
      ...(this.lookups()?.departments ?? []).map((d) => ({
        value: d.id,
        label: lang === 'en' ? d.nameEn || d.nameAr : d.nameAr,
      })),
    ];
  });
  protected readonly jobOptions = computed((): SelectOption[] => {
    const lang = this.i18n.getActiveLang();
    return [
      { value: '', label: '—' },
      ...(this.lookups()?.jobTitles ?? []).map((j) => ({
        value: j.id,
        label: lang === 'en' ? j.nameEn || j.nameAr : j.nameAr,
      })),
    ];
  });

  protected genderOptions(): SelectOption[] {
    return [
      { value: '', label: '—' },
      { value: 'Male', label: this.i18n.translate('employees.genderMale') },
      { value: 'Female', label: this.i18n.translate('employees.genderFemale') },
      { value: 'Unspecified', label: this.i18n.translate('employees.genderUnspecified') },
    ];
  }

  protected contractOptions(): SelectOption[] {
    return [
      { value: 'Unlimited', label: this.i18n.translate('employees.contractUnlimited') },
      { value: 'FixedTerm', label: this.i18n.translate('employees.contractFixed') },
      { value: 'Other', label: this.i18n.translate('employees.contractOther') },
    ];
  }

  protected statusOptions(): SelectOption[] {
    return [
      { value: 'Active', label: this.i18n.translate('employees.status.active') },
      { value: 'Probation', label: this.i18n.translate('employees.status.probation') },
    ];
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEdit.set(true);
      this.employeeId.set(id);
    }
    void this.reload();
  }

  ngOnDestroy(): void {
    this.dirty.clear(DIRTY_ID);
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty.hasDirty()) {
      event.preventDefault();
    }
  }

  reload(): void {
    void this.load();
  }

  toggle(key: 'personal' | 'contact' | 'emergency' | 'job'): void {
    if (typeof matchMedia !== 'undefined' && matchMedia('(min-width: 768px)').matches) {
      return;
    }
    this.open.update((o) => ({ ...o, [key]: !o[key] }));
  }

  setField(key: string, value: string): void {
    const map: Record<string, (v: string) => void> = {
      fullNameAr: (v) => this.fullNameAr.set(v),
      fullNameEn: (v) => this.fullNameEn.set(v),
      nationalId: (v) => this.nationalId.set(v),
      gender: (v) => this.gender.set(v),
      dateOfBirth: (v) => this.dateOfBirth.set(v),
      email: (v) => this.email.set(v),
      phone: (v) => this.phone.set(v),
      address: (v) => this.address.set(v),
      branchId: (v) => this.branchId.set(v),
      departmentId: (v) => this.departmentId.set(v),
      jobTitleId: (v) => this.jobTitleId.set(v),
      managerEmployeeId: (v) => this.managerEmployeeId.set(v),
      hireDate: (v) => this.hireDate.set(v),
      probationEndDate: (v) => this.probationEndDate.set(v),
      contractType: (v) => this.contractType.set(v),
      status: (v) => this.status.set(v),
    };
    map[key]?.(value);
    this.dirty.register(DIRTY_ID);
  }

  patchEmergency(index: number, field: keyof EmergencyContact, value: string): void {
    this.emergency.update((list) =>
      list.map((c, i) => (i === index ? { ...c, [field]: value } : c)),
    );
    this.dirty.register(DIRTY_ID);
  }

  addEmergency(): void {
    this.emergency.update((list) => [...list, { name: '', relationship: '', phone: '' }]);
    this.dirty.register(DIRTY_ID);
  }

  goPlans(): void {
    this.showCapDialog.set(false);
    void this.router.navigateByUrl('/settings/subscription');
  }

  confirmLeave(): void {
    this.showLeaveDialog.set(false);
    this.dirty.clear(DIRTY_ID);
    if (this.leaveUrl) {
      void this.router.navigateByUrl(this.leaveUrl);
    }
  }

  async save(): Promise<void> {
    this.formError.set(null);
    if (!this.fullNameAr().trim() || !this.hireDate()) {
      this.formError.set(this.i18n.translate('employees.validationRequired'));
      return;
    }
    this.saving.set(true);
    const contacts = this.emergency().filter((c) => c.name.trim() && c.phone.trim());
    const body: CreateEmployeeBody = {
      fullNameAr: this.fullNameAr().trim(),
      fullNameEn: this.fullNameEn().trim() || null,
      nationalId: this.nationalId().trim() || null,
      gender: this.gender() || null,
      dateOfBirth: this.dateOfBirth() || null,
      email: this.email().trim() || null,
      phone: this.phone().trim() || null,
      address: this.address().trim() || null,
      branchId: this.branchId() || null,
      departmentId: this.departmentId() || null,
      jobTitleId: this.jobTitleId() || null,
      managerEmployeeId: this.managerEmployeeId() || null,
      hireDate: this.hireDate(),
      probationEndDate: this.probationEndDate() || null,
      contractType: this.contractType() || null,
      status: this.isEdit() ? undefined : this.status(),
      emergencyContacts: contacts,
    };
    try {
      if (this.isEdit() && this.employeeId()) {
        const updated = await this.api.update(this.employeeId()!, {
          ...body,
          branchId: this.branchId() || body.branchId!,
        });
        this.dirty.clear(DIRTY_ID);
        this.toast.show(this.i18n.translate('employees.saved'), 'success');
        void this.router.navigate(['/employees', updated.id]);
      } else {
        const created = await this.api.create(body);
        this.dirty.clear(DIRTY_ID);
        this.toast.show(this.i18n.translate('employees.created'), 'success');
        void this.router.navigate(['/employees', created.id]);
      }
    } catch (err: unknown) {
      const code = (err as { error?: { code?: string } })?.error?.code;
      if (code === 'plan.employee_cap_exceeded' || code === 'plan.feature_not_available') {
        if (code === 'plan.employee_cap_exceeded') {
          this.showCapDialog.set(true);
        } else {
          this.state.set('locked');
        }
      } else {
        this.formError.set(this.i18n.translate('employees.saveError'));
      }
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      const [lookups, managers] = await Promise.all([
        this.api.orgLookups(),
        this.api.list({ page: 1, pageSize: 100 }),
      ]);
      this.lookups.set(lookups);
      if (!this.branchId() && lookups.branches[0]) {
        this.branchId.set(lookups.branches[0].id);
      }
      this.managerOptions.set([
        { value: '', label: '—' },
        ...managers.items
          .filter((m) => m.id !== this.employeeId())
          .map((m) => ({
            value: m.id,
            label:
              this.i18n.getActiveLang() === 'en' && m.fullNameEn
                ? m.fullNameEn
                : m.fullNameAr,
          })),
      ]);

      if (this.isEdit() && this.employeeId()) {
        const e = await this.api.get(this.employeeId()!);
        this.fullNameAr.set(e.fullNameAr);
        this.fullNameEn.set(e.fullNameEn ?? '');
        this.nationalId.set(e.nationalId ?? '');
        this.gender.set(e.gender ?? '');
        this.dateOfBirth.set(e.dateOfBirth ?? '');
        this.email.set(e.email ?? '');
        this.phone.set(e.phone ?? '');
        this.address.set(e.address ?? '');
        this.branchId.set(e.branchId);
        this.departmentId.set(e.departmentId ?? '');
        this.jobTitleId.set(e.jobTitleId ?? '');
        this.managerEmployeeId.set(e.managerEmployeeId ?? '');
        this.hireDate.set(e.hireDate);
        this.probationEndDate.set(e.probationEndDate ?? '');
        this.contractType.set(e.contractType || 'Unlimited');
        this.status.set(e.status);
        this.employeeNumber.set(e.employeeNumber);
        this.emergency.set(
          e.emergencyContacts.length
            ? e.emergencyContacts.map((c) => ({
                name: c.name,
                relationship: c.relationship,
                phone: c.phone,
              }))
            : [{ name: '', relationship: '', phone: '' }],
        );
      }
      this.dirty.clear(DIRTY_ID);
      this.state.set('ready');
    } catch (err: unknown) {
      const code = (err as { error?: { code?: string } })?.error?.code;
      this.state.set(code === 'plan.feature_not_available' ? 'locked' : 'error');
    }
  }
}
