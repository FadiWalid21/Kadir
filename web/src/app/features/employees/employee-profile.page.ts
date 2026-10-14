import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  DocumentTypeLookup,
  EmployeeDetail,
  EmployeeDocumentItem,
  EmployeesApi,
} from '../../core/api/employees.api';
import { AttendanceApi, EmployeeAttendancePolicy } from '../../core/api/attendance.api';
import { UsersApi } from '../../core/api/users.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { SelectComponent, SelectOption } from '../../shared/ui/select/select.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { TabsComponent, TabItem } from '../../shared/ui/tabs/tabs.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { EmployeeSalaryPanelComponent } from './employee-salary-panel.component';

type PageState = 'loading' | 'ready' | 'error' | 'locked';

@Component({
  selector: 'kadr-employee-profile-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    AvatarComponent,
    BadgeComponent,
    ButtonComponent,
    TabsComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DrawerComponent,
    InputComponent,
    SelectComponent,
    ConfirmDialogComponent,
    EmployeeSalaryPanelComponent,
  ],
  template: `
    <div class="flex flex-col gap-5">
      @if (state() === 'loading') {
        <kadr-skeleton height="6rem" />
        <kadr-skeleton height="12rem" />
      } @else if (state() === 'locked') {
        <kadr-empty-state
          [title]="'employees.featureLockedTitle' | transloco"
          [description]="'employees.featureLockedBody' | transloco"
        />
      } @else if (state() === 'error' || !employee()) {
        <kadr-empty-state
          [title]="'employees.loadErrorTitle' | transloco"
          [description]="'employees.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="reload()">{{
            'home.retry' | transloco
          }}</kadr-button>
        </kadr-empty-state>
      } @else {
        @if (employee(); as e) {
          <nav class="text-sm text-fg-2">
            <a routerLink="/employees" class="text-accent hover:underline">{{
              'employees.title' | transloco
            }}</a>
            <span class="mx-1">/</span>
            <span>{{ displayName(e) }}</span>
          </nav>

          <header
            class="flex flex-col gap-4 rounded-lg border border-line bg-surface p-4 sm:flex-row sm:items-start sm:justify-between"
          >
            <div class="flex min-w-0 items-start gap-3">
              <kadr-avatar [name]="e.fullNameAr" size="lg" [ariaLabel]="e.fullNameAr" />
              <div class="min-w-0 text-start">
                <div class="flex flex-wrap items-center gap-2">
                  <h1 class="text-2xl font-bold text-fg">{{ displayName(e) }}</h1>
                  <kadr-badge [tone]="statusTone(e.status)" [label]="statusLabel(e.status)" />
                </div>
                <p class="mt-1 text-sm text-fg-2">
                  <bdi>{{ e.employeeNumber }}</bdi>
                  · {{ jobName(e) }}
                  · {{ deptName(e) }}
                  · {{ e.branchName }}
                </p>
                <p class="mt-1 text-xs text-fg-muted">
                  {{ 'employees.hiredOn' | transloco: { date: e.hireDate } }}
                </p>
              </div>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              @if (canUpdate()) {
                <a [routerLink]="['/employees', e.id, 'edit']" class="contents">
                  <kadr-button variant="secondary" type="button">{{
                    'employees.edit' | transloco
                  }}</kadr-button>
                </a>
              }
              @if (canTerminate() && e.status !== 'Terminated') {
                <kadr-button variant="danger-soft" type="button" (click)="askTerminate()">
                  {{ 'employees.terminate' | transloco }}
                </kadr-button>
              }
            </div>
          </header>

          <kadr-tabs
            [tabs]="tabs()"
            [ariaLabel]="'employees.profileTabs' | transloco"
            [(activeId)]="activeTab"
          />

          @if (activeTab() === 'overview') {
            <div class="grid gap-4 lg:grid-cols-2">
              <section class="rounded-lg border border-line bg-surface p-4 text-start">
                <h2 class="text-sm font-bold text-fg">{{ 'employees.sectionPersonal' | transloco }}</h2>
                <dl class="mt-3 grid gap-2 text-sm">
                  <div class="flex justify-between gap-2">
                    <dt class="text-fg-2">{{ 'employees.nationalId' | transloco }}</dt>
                    <dd class="tabular-nums"><bdi>{{ e.nationalIdMasked || '—' }}</bdi></dd>
                  </div>
                  <div class="flex justify-between gap-2">
                    <dt class="text-fg-2">{{ 'employees.gender' | transloco }}</dt>
                    <dd>{{ e.gender || '—' }}</dd>
                  </div>
                  <div class="flex justify-between gap-2">
                    <dt class="text-fg-2">{{ 'employees.dateOfBirth' | transloco }}</dt>
                    <dd class="tabular-nums">{{ e.dateOfBirth || '—' }}</dd>
                  </div>
                </dl>
              </section>
              <section class="rounded-lg border border-line bg-surface p-4 text-start">
                <h2 class="text-sm font-bold text-fg">{{ 'employees.sectionContact' | transloco }}</h2>
                <dl class="mt-3 grid gap-2 text-sm">
                  <div class="flex justify-between gap-2">
                    <dt class="text-fg-2">{{ 'ui.email' | transloco }}</dt>
                    <dd><bdi>{{ e.email || '—' }}</bdi></dd>
                  </div>
                  <div class="flex justify-between gap-2">
                    <dt class="text-fg-2">{{ 'employees.phone' | transloco }}</dt>
                    <dd><bdi>{{ e.phone || '—' }}</bdi></dd>
                  </div>
                  <div class="flex justify-between gap-2">
                    <dt class="text-fg-2">{{ 'employees.address' | transloco }}</dt>
                    <dd class="text-end">{{ e.address || '—' }}</dd>
                  </div>
                </dl>
              </section>
              @if (canManageAttendance()) {
                <section class="rounded-lg border border-line bg-surface p-4 text-start lg:col-span-2">
                  <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div class="min-w-0 text-start">
                      <h2 class="text-sm font-bold text-fg">
                        {{ 'employees.sectionAttendancePolicy' | transloco }}
                      </h2>
                      <p class="mt-1 text-sm text-fg-2">{{ 'employees.attendancePolicyHint' | transloco }}</p>
                    </div>
                    <kadr-button
                      variant="secondary"
                      type="button"
                      [loading]="policySaving()"
                      (click)="saveAttendancePolicy()"
                    >
                      {{ 'ui.save' | transloco }}
                    </kadr-button>
                  </div>
                  @if (policyLoadError()) {
                    <p class="mt-2 text-sm text-danger" role="alert">{{ policyLoadError() }}</p>
                  } @else if (attendancePolicy(); as pol) {
                    <div class="mt-3 flex flex-col gap-2 text-sm">
                      <label class="flex items-center gap-2 text-fg">
                        <input
                          type="checkbox"
                          [checked]="pol.mayClockInFromAnywhere"
                          (change)="patchPolicy({ mayClockInFromAnywhere: $any($event.target).checked })"
                        />
                        {{ 'employees.mayClockAnywhere' | transloco }}
                      </label>
                      <label
                        class="flex items-center gap-2 text-fg"
                        [class.opacity-50]="!pol.mayClockInFromAnywhere"
                      >
                        <input
                          type="checkbox"
                          [disabled]="!pol.mayClockInFromAnywhere"
                          [checked]="pol.anywhereRequiresReason"
                          (change)="patchPolicy({ anywhereRequiresReason: $any($event.target).checked })"
                        />
                        {{ 'employees.anywhereRequiresReason' | transloco }}
                      </label>
                      <label
                        class="flex items-center gap-2 text-fg"
                        [class.opacity-50]="!pol.mayClockInFromAnywhere"
                      >
                        <input
                          type="checkbox"
                          [disabled]="!pol.mayClockInFromAnywhere"
                          [checked]="pol.anywhereRequiresPhoto"
                          (change)="patchPolicy({ anywhereRequiresPhoto: $any($event.target).checked })"
                        />
                        {{ 'employees.anywhereRequiresPhoto' | transloco }}
                      </label>
                    </div>
                  } @else {
                    <p class="mt-2 text-sm text-fg-muted">{{ 'ui.loading' | transloco }}</p>
                  }
                </section>
              }

              <section class="rounded-lg border border-line bg-surface p-4 text-start lg:col-span-2">
                <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div class="min-w-0 text-start">
                    <h2 class="text-sm font-bold text-fg">{{ 'employees.sectionLogin' | transloco }}</h2>
                    <p class="mt-1 text-sm text-fg-2">{{ 'employees.loginHint' | transloco }}</p>
                    @if (e.userId && e.linkedUserEmail) {
                      <p class="mt-2 text-sm text-fg">
                        <span class="font-medium">{{ e.linkedUserFullName }}</span>
                        <span class="text-fg-2"> · </span>
                        <bdi dir="ltr">{{ e.linkedUserEmail }}</bdi>
                      </p>
                    } @else {
                      <p class="mt-2 text-sm text-fg-muted">{{ 'employees.loginNotLinked' | transloco }}</p>
                    }
                  </div>
                  @if (canLinkUser()) {
                    <div class="flex flex-wrap gap-2">
                      <kadr-button variant="secondary" type="button" (click)="openUserLink()">
                        {{ (e.userId ? 'employees.changeLoginLink' : 'employees.linkLogin') | transloco }}
                      </kadr-button>
                      @if (e.userId) {
                        <kadr-button variant="secondary" type="button" [loading]="linkBusy()" (click)="unlinkLogin()">
                          {{ 'employees.unlinkLogin' | transloco }}
                        </kadr-button>
                      }
                    </div>
                  }
                </div>
              </section>
              <section class="rounded-lg border border-line bg-surface p-4 text-start lg:col-span-2">
                <h2 class="text-sm font-bold text-fg">{{ 'employees.sectionEmergency' | transloco }}</h2>
                @if (e.emergencyContacts.length === 0) {
                  <p class="mt-2 text-sm text-fg-2">{{ 'employees.noEmergency' | transloco }}</p>
                } @else {
                  <ul class="mt-3 flex flex-col gap-2">
                    @for (c of e.emergencyContacts; track c.id ?? c.phone) {
                      <li class="text-sm">
                        <span class="font-medium">{{ c.name }}</span>
                        <span class="text-fg-2"> · {{ c.relationship }} · </span>
                        <bdi>{{ c.phone }}</bdi>
                      </li>
                    }
                  </ul>
                }
              </section>
              @if (e.urgentDocuments.length) {
                <section class="rounded-lg border border-line bg-surface p-4 text-start lg:col-span-2">
                  <h2 class="text-sm font-bold text-fg">{{ 'employees.urgentDocs' | transloco }}</h2>
                  <ul class="mt-3 flex flex-col gap-2">
                    @for (d of e.urgentDocuments; track d.id) {
                      <li class="flex flex-wrap items-center justify-between gap-2 text-sm">
                        <span>{{ docTypeName(d) }}</span>
                        <kadr-badge
                          [tone]="expiryTone(d.expiryStatus)"
                          [label]="expiryLabel(d.expiryStatus)"
                        />
                      </li>
                    }
                  </ul>
                </section>
              }
              @if (e.directReports.length) {
                <section class="rounded-lg border border-line bg-surface p-4 text-start lg:col-span-2">
                  <h2 class="text-sm font-bold text-fg">{{ 'employees.directReports' | transloco }}</h2>
                  <ul class="mt-3 flex flex-col gap-2">
                    @for (r of e.directReports; track r.id) {
                      <li>
                        <a [routerLink]="['/employees', r.id]" class="text-sm text-accent hover:underline">{{
                          r.fullNameAr
                        }}</a>
                      </li>
                    }
                  </ul>
                </section>
              }
            </div>
          }

          @if (activeTab() === 'job') {
            <section class="rounded-lg border border-line bg-surface p-4 text-start">
              <h2 class="text-sm font-bold text-fg">{{ 'employees.sectionJob' | transloco }}</h2>
              <dl class="mt-3 grid gap-2 text-sm sm:grid-cols-2">
                <div class="flex justify-between gap-2">
                  <dt class="text-fg-2">{{ 'glossary.jobTitle' | transloco }}</dt>
                  <dd>{{ jobName(e) }}</dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-fg-2">{{ 'glossary.department' | transloco }}</dt>
                  <dd>{{ deptName(e) }}</dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-fg-2">{{ 'employees.manager' | transloco }}</dt>
                  <dd>{{ e.managerNameAr || '—' }}</dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-fg-2">{{ 'employees.hireDate' | transloco }}</dt>
                  <dd class="tabular-nums">{{ e.hireDate }}</dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-fg-2">{{ 'employees.probationEnd' | transloco }}</dt>
                  <dd class="tabular-nums">{{ e.probationEndDate || '—' }}</dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-fg-2">{{ 'employees.contractType' | transloco }}</dt>
                  <dd>{{ e.contractType }}</dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-fg-2">{{ 'employees.contractStart' | transloco }}</dt>
                  <dd class="tabular-nums">{{ e.contractStartDate || '—' }}</dd>
                </div>
                <div class="flex justify-between gap-2">
                  <dt class="text-fg-2">{{ 'employees.contractEnd' | transloco }}</dt>
                  <dd class="tabular-nums">{{ e.contractEndDate || '—' }}</dd>
                </div>
              </dl>
              @if (canUpdate() || canTerminate()) {
                <div class="mt-4 flex flex-wrap items-end gap-2 border-t border-line pt-4">
                  <kadr-select
                    [label]="'employees.colStatus' | transloco"
                    [options]="statusChangeOptions()"
                    [value]="statusDraft()"
                    (valueChange)="statusDraft.set($event)"
                  />
                  <kadr-button
                    variant="secondary"
                    type="button"
                    [loading]="statusSaving()"
                    (click)="saveStatus()"
                  >
                    {{ 'employees.changeStatus' | transloco }}
                  </kadr-button>
                </div>
              }
            </section>
          }

          @if (activeTab() === 'salary' && employee(); as salEmp) {
            <kadr-employee-salary-panel [employeeId]="salEmp.id" />
          }

          @if (activeTab() === 'documents') {
            <div class="flex flex-col gap-4">
              <div class="flex flex-wrap items-center justify-between gap-2">
                <div class="flex flex-wrap gap-2">
                  <span class="rounded-full bg-success-soft px-2 py-0.5 text-xs text-success">{{
                    'employees.docValid' | transloco: { n: docCounts().valid }
                  }}</span>
                  <span class="rounded-full bg-warning-soft px-2 py-0.5 text-xs text-warning">{{
                    'employees.docSoon' | transloco: { n: docCounts().soon }
                  }}</span>
                  <span class="rounded-full bg-danger-soft px-2 py-0.5 text-xs text-danger">{{
                    'employees.docExpired' | transloco: { n: docCounts().expired }
                  }}</span>
                </div>
                @if (canManageDocs()) {
                  <kadr-button variant="primary" type="button" (click)="openUpload()">{{
                    'employees.uploadDoc' | transloco
                  }}</kadr-button>
                }
              </div>
              @if (docs().length === 0) {
                <kadr-empty-state
                  [title]="'employees.docsEmptyTitle' | transloco"
                  [description]="'employees.docsEmptyBody' | transloco"
                />
              } @else {
                <div class="overflow-x-auto rounded-lg border border-line bg-surface">
                  <table class="w-full text-start text-sm">
                    <thead class="border-b border-line bg-subtle text-xs text-fg-2">
                      <tr>
                        <th class="px-3 py-3 font-medium">{{ 'employees.docType' | transloco }}</th>
                        <th class="px-3 py-3 font-medium">{{ 'employees.docRef' | transloco }}</th>
                        <th class="px-3 py-3 font-medium">{{ 'employees.docIssued' | transloco }}</th>
                        <th class="px-3 py-3 font-medium">{{ 'employees.docExpires' | transloco }}</th>
                        <th class="px-3 py-3 font-medium">{{ 'employees.colStatus' | transloco }}</th>
                        <th class="px-3 py-3 font-medium">{{ 'employees.docFile' | transloco }}</th>
                        <th class="px-3 py-3 font-medium"></th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (d of docs(); track d.id) {
                        <tr class="border-b border-line">
                          <td class="px-3 py-3">{{ docTypeName(d) }}</td>
                          <td class="px-3 py-3"><bdi>{{ d.referenceNumber || '—' }}</bdi></td>
                          <td class="px-3 py-3 tabular-nums">{{ d.issuedOn || '—' }}</td>
                          <td class="px-3 py-3 tabular-nums">{{ d.expiresOn || '—' }}</td>
                          <td class="px-3 py-3">
                            <kadr-badge
                              [tone]="expiryTone(d.expiryStatus)"
                              [label]="expiryLabel(d.expiryStatus)"
                            />
                          </td>
                          <td class="px-3 py-3">
                            <button
                              type="button"
                              class="text-accent hover:underline"
                              (click)="openFile(d)"
                            >
                              {{ d.fileName || ('employees.openFile' | transloco) }}
                            </button>
                          </td>
                          <td class="px-3 py-3">
                            @if (canManageDocs()) {
                              <kadr-button
                                variant="danger-soft"
                                size="sm"
                                type="button"
                                (click)="askDelete(d)"
                              >
                                {{ 'ui.delete' | transloco }}
                              </kadr-button>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          }

          @if (activeTab() === 'attendance' || activeTab() === 'leave' || activeTab() === 'audit') {
            <kadr-empty-state
              [title]="'employees.comingLaterTitle' | transloco"
              [description]="'employees.comingLaterBody' | transloco"
            />
          }
        }
      }

      <kadr-drawer
        [open]="uploadOpen()"
        [title]="'employees.uploadDoc' | transloco"
        [subtitle]="'employees.uploadHint' | transloco"
        [closeLabel]="'ui.close' | transloco"
        (closed)="uploadOpen.set(false)"
      >
        <div class="flex flex-col gap-3 p-4">
          <kadr-select
            [label]="'employees.docType' | transloco"
            [options]="docTypeOptions()"
            [value]="uploadTypeId()"
            (valueChange)="onTypeChange($event)"
          />
          <kadr-input
            [label]="'employees.docRef' | transloco"
            [value]="uploadRef()"
            (valueChange)="uploadRef.set($event)"
          />
          <label class="grid gap-2 text-sm font-medium text-fg">
            <span>{{ 'employees.docIssued' | transloco }}</span>
            <input
              type="date"
              class="h-control rounded-md border border-line-strong bg-surface px-3"
              [ngModel]="uploadIssued()"
              (ngModelChange)="uploadIssued.set($event)"
            />
          </label>
          @if (selectedType()?.hasExpiry) {
            <label class="grid gap-2 text-sm font-medium text-fg">
              <span>{{ 'employees.docExpires' | transloco }} *</span>
              <input
                type="date"
                class="h-control rounded-md border border-line-strong bg-surface px-3"
                [ngModel]="uploadExpires()"
                (ngModelChange)="uploadExpires.set($event)"
              />
            </label>
          }
          <label class="grid gap-2 text-sm font-medium text-fg">
            <span>{{ 'employees.docFile' | transloco }} *</span>
            <input type="file" (change)="onFile($event)" />
          </label>
          <p class="text-xs text-fg-muted">{{ 'employees.uploadAlertNote' | transloco }}</p>
          @if (uploadError()) {
            <p class="text-sm text-danger" role="alert">{{ uploadError() }}</p>
          }
          <div class="flex justify-end gap-2">
            <kadr-button variant="secondary" type="button" (click)="uploadOpen.set(false)">{{
              'ui.cancel' | transloco
            }}</kadr-button>
            <kadr-button
              variant="primary"
              type="button"
              [loading]="uploading()"
              (click)="submitUpload()"
            >
              {{ 'ui.save' | transloco }}
            </kadr-button>
          </div>
        </div>
      </kadr-drawer>

      <kadr-drawer
        [open]="userLinkOpen()"
        [title]="'employees.linkLoginTitle' | transloco"
        [closeLabel]="'ui.cancel' | transloco"
        (closed)="userLinkOpen.set(false)"
      >
        <p class="mb-3 text-sm text-fg-2">{{ 'employees.linkLoginBody' | transloco }}</p>
        <kadr-select
          [label]="'employees.loginUser' | transloco"
          [options]="userOptions()"
          [value]="linkUserId()"
          (valueChange)="linkUserId.set($event)"
        />
        <div class="mt-4 flex justify-end gap-2">
          <kadr-button variant="secondary" type="button" (click)="userLinkOpen.set(false)">{{
            'ui.cancel' | transloco
          }}</kadr-button>
          <kadr-button variant="primary" type="button" [loading]="linkBusy()" (click)="saveUserLink()">
            {{ 'employees.saveLoginLink' | transloco }}
          </kadr-button>
        </div>
      </kadr-drawer>

      <kadr-confirm-dialog
        [open]="!!deleteTarget()"
        [title]="'employees.deleteDocTitle' | transloco"
        [message]="'employees.deleteDocBody' | transloco"
        [confirmLabel]="'ui.delete' | transloco"
        [cancelLabel]="'ui.cancel' | transloco"
        [danger]="true"
        [busy]="deleting()"
        (confirmed)="confirmDelete()"
        (cancelled)="deleteTarget.set(null)"
      />

      <kadr-confirm-dialog
        [open]="terminateOpen()"
        [title]="'employees.terminateTitle' | transloco"
        [message]="'employees.terminateBody' | transloco"
        [confirmLabel]="'employees.terminateConfirm' | transloco"
        [cancelLabel]="'ui.cancel' | transloco"
        [danger]="true"
        [busy]="terminating()"
        (confirmed)="confirmTerminate()"
        (cancelled)="terminateOpen.set(false)"
      />
    </div>
  `,
})
export class EmployeeProfilePage implements OnInit {
  private readonly api = inject(EmployeesApi);
  private readonly attendanceApi = inject(AttendanceApi);
  private readonly usersApi = inject(UsersApi);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(TranslocoService);
  private readonly toast = inject(ToastService);

  protected readonly state = signal<PageState>('loading');
  protected readonly employee = signal<EmployeeDetail | null>(null);
  protected readonly attendancePolicy = signal<EmployeeAttendancePolicy | null>(null);
  protected readonly policySaving = signal(false);
  protected readonly policyLoadError = signal<string | null>(null);
  protected readonly docs = signal<EmployeeDocumentItem[]>([]);
  protected readonly docTypes = signal<DocumentTypeLookup[]>([]);
  protected readonly activeTab = signal('overview');
  protected readonly statusDraft = signal('');
  protected readonly statusSaving = signal(false);
  protected readonly terminateOpen = signal(false);
  protected readonly terminating = signal(false);
  protected readonly userLinkOpen = signal(false);
  protected readonly linkUserId = signal('');
  protected readonly userOptions = signal<SelectOption[]>([]);
  protected readonly linkBusy = signal(false);

  protected readonly uploadOpen = signal(false);
  protected readonly uploadTypeId = signal('');
  protected readonly uploadRef = signal('');
  protected readonly uploadIssued = signal('');
  protected readonly uploadExpires = signal('');
  protected readonly uploadFile = signal<File | null>(null);
  protected readonly uploadError = signal<string | null>(null);
  protected readonly uploading = signal(false);
  protected readonly deleteTarget = signal<EmployeeDocumentItem | null>(null);
  protected readonly deleting = signal(false);

  protected readonly tabs = computed((): TabItem[] => {
    const items: TabItem[] = [
      { id: 'overview', label: this.i18n.translate('employees.tabOverview') },
      { id: 'job', label: this.i18n.translate('employees.tabJob') },
      { id: 'documents', label: this.i18n.translate('employees.tabDocuments') },
    ];
    if (this.auth.hasPermission(PermissionCodes.SalaryRead)) {
      items.push({ id: 'salary', label: this.i18n.translate('employees.tabSalary') });
    }
    items.push(
      { id: 'attendance', label: this.i18n.translate('employees.tabAttendance') },
      { id: 'leave', label: this.i18n.translate('employees.tabLeave') },
      { id: 'audit', label: this.i18n.translate('employees.tabAudit') },
    );
    return items;
  });

  protected readonly docCounts = computed(() => {
    const items = this.docs();
    return {
      valid: items.filter((d) => d.expiryStatus === 'valid' || d.expiryStatus === 'none').length,
      soon: items.filter((d) => d.expiryStatus === 'soon').length,
      expired: items.filter((d) => d.expiryStatus === 'expired').length,
    };
  });

  protected readonly selectedType = computed(() =>
    this.docTypes().find((t) => t.id === this.uploadTypeId()) ?? null,
  );

  protected readonly docTypeOptions = computed((): SelectOption[] => {
    const lang = this.i18n.getActiveLang();
    return this.docTypes().map((t) => ({
      value: t.id,
      label: lang === 'en' ? t.nameEn || t.nameAr : t.nameAr,
    }));
  });

  ngOnInit(): void {
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (
      tab &&
      (tab !== 'salary' || this.auth.hasPermission(PermissionCodes.SalaryRead))
    ) {
      this.activeTab.set(tab);
    }
    void this.reload();
  }

  canUpdate(): boolean {
    return this.auth.hasPermission(PermissionCodes.EmployeesUpdate);
  }

  canTerminate(): boolean {
    return this.auth.hasPermission(PermissionCodes.EmployeesTerminate);
  }

  canManageDocs(): boolean {
    return this.auth.hasPermission(PermissionCodes.EmployeesDocumentsManage);
  }

  canLinkUser(): boolean {
    return this.auth.hasPermission(PermissionCodes.UsersUpdate);
  }

  canManageAttendance(): boolean {
    return this.auth.hasPermission(PermissionCodes.AttendanceManage);
  }

  patchPolicy(patch: Partial<EmployeeAttendancePolicy>): void {
    const current = this.attendancePolicy();
    if (!current) {
      return;
    }
    this.attendancePolicy.set({ ...current, ...patch });
  }

  async saveAttendancePolicy(): Promise<void> {
    const e = this.employee();
    const pol = this.attendancePolicy();
    if (!e || !pol) {
      return;
    }
    this.policySaving.set(true);
    try {
      const saved = await this.attendanceApi.updateEmployeeAttendancePolicy(e.id, {
        mayClockInFromAnywhere: pol.mayClockInFromAnywhere,
        anywhereRequiresReason: pol.anywhereRequiresReason,
        anywhereRequiresPhoto: pol.anywhereRequiresPhoto,
      });
      this.attendancePolicy.set(saved);
      this.toast.show(this.i18n.translate('employees.attendancePolicySaved'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('employees.attendancePolicyFailed'), 'danger');
    } finally {
      this.policySaving.set(false);
    }
  }

  async openUserLink(): Promise<void> {
    const e = this.employee();
    if (!e) {
      return;
    }
    this.linkUserId.set(e.userId ?? '');
    this.userLinkOpen.set(true);
    try {
      const page = await this.usersApi.list({ page: 1, pageSize: 100, active: true });
      this.userOptions.set([
        { value: '', label: this.i18n.translate('employees.pickLoginUser') },
        ...page.items.map((u) => ({
          value: u.id,
          label: `${u.fullName} (${u.email})`,
        })),
      ]);
    } catch {
      this.userOptions.set([{ value: '', label: this.i18n.translate('employees.pickLoginUser') }]);
      this.toast.show(this.i18n.translate('employees.linkLoginFailed'), 'danger');
    }
  }

  async saveUserLink(): Promise<void> {
    const e = this.employee();
    const userId = this.linkUserId().trim();
    if (!e || !userId) {
      this.toast.show(this.i18n.translate('employees.pickLoginRequired'), 'danger');
      return;
    }
    this.linkBusy.set(true);
    try {
      if (e.userId && e.userId !== userId) {
        await this.usersApi.setEmployeeLink(e.userId, null);
      }
      await this.usersApi.setEmployeeLink(userId, e.id);
      this.userLinkOpen.set(false);
      this.toast.show(this.i18n.translate('employees.loginLinked'), 'success');
      await this.load();
    } catch {
      this.toast.show(this.i18n.translate('employees.linkLoginFailed'), 'danger');
    } finally {
      this.linkBusy.set(false);
    }
  }

  async unlinkLogin(): Promise<void> {
    const e = this.employee();
    if (!e?.userId) {
      return;
    }
    this.linkBusy.set(true);
    try {
      await this.usersApi.setEmployeeLink(e.userId, null);
      this.toast.show(this.i18n.translate('employees.loginUnlinked'), 'success');
      await this.load();
    } catch {
      this.toast.show(this.i18n.translate('employees.linkLoginFailed'), 'danger');
    } finally {
      this.linkBusy.set(false);
    }
  }

  reload(): void {
    void this.load();
  }

  displayName(e: EmployeeDetail): string {
    return this.i18n.getActiveLang() === 'en' && e.fullNameEn ? e.fullNameEn : e.fullNameAr;
  }

  jobName(e: EmployeeDetail): string {
    return (this.i18n.getActiveLang() === 'ar' ? e.jobTitleNameAr : e.jobTitleNameEn) || '—';
  }

  deptName(e: EmployeeDetail): string {
    return (this.i18n.getActiveLang() === 'ar' ? e.departmentNameAr : e.departmentNameEn) || '—';
  }

  docTypeName(d: EmployeeDocumentItem): string {
    return this.i18n.getActiveLang() === 'en' ? d.documentTypeNameEn || d.documentTypeNameAr : d.documentTypeNameAr;
  }

  statusTone(status: string): BadgeTone {
    switch (status) {
      case 'Active':
        return 'success';
      case 'Probation':
        return 'info';
      case 'Suspended':
        return 'warning';
      default:
        return 'neutral';
    }
  }

  statusLabel(status: string): string {
    const map: Record<string, string> = {
      Active: 'employees.status.active',
      Probation: 'employees.status.probation',
      OnLeave: 'employees.status.onLeave',
      Suspended: 'employees.status.suspended',
      Terminated: 'employees.status.terminated',
    };
    return this.i18n.translate(map[status] ?? status);
  }

  expiryTone(s: string): BadgeTone {
    if (s === 'expired') return 'danger';
    if (s === 'soon') return 'warning';
    if (s === 'valid') return 'success';
    return 'neutral';
  }

  expiryLabel(s: string): string {
    const map: Record<string, string> = {
      valid: 'employees.expiry.valid',
      soon: 'employees.expiry.soon',
      expired: 'employees.expiry.expired',
      none: 'employees.expiry.none',
    };
    return this.i18n.translate(map[s] ?? s);
  }

  statusChangeOptions(): SelectOption[] {
    const all = [
      { value: 'Active', label: this.i18n.translate('employees.status.active') },
      { value: 'Probation', label: this.i18n.translate('employees.status.probation') },
      { value: 'OnLeave', label: this.i18n.translate('employees.status.onLeave') },
      { value: 'Suspended', label: this.i18n.translate('employees.status.suspended') },
      { value: 'Terminated', label: this.i18n.translate('employees.status.terminated') },
    ];
    if (!this.canTerminate()) {
      return all.filter((o) => o.value !== 'Suspended' && o.value !== 'Terminated');
    }
    return all;
  }

  async openFile(d: EmployeeDocumentItem): Promise<void> {
    const e = this.employee();
    if (!e) return;
    try {
      const blob = await this.api.downloadDocument(e.id, d.id);
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank', 'noopener');
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch {
      this.toast.show(this.i18n.translate('employees.saveError'), 'danger');
    }
  }

  openUpload(): void {
    this.uploadError.set(null);
    this.uploadRef.set('');
    this.uploadIssued.set('');
    this.uploadExpires.set('');
    this.uploadFile.set(null);
    if (this.docTypes()[0]) this.uploadTypeId.set(this.docTypes()[0]!.id);
    this.uploadOpen.set(true);
  }

  onTypeChange(id: string): void {
    this.uploadTypeId.set(id);
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.uploadFile.set(input.files?.[0] ?? null);
  }

  askDelete(d: EmployeeDocumentItem): void {
    this.deleteTarget.set(d);
  }

  askTerminate(): void {
    this.terminateOpen.set(true);
  }

  async confirmTerminate(): Promise<void> {
    const e = this.employee();
    if (!e || this.terminating()) {
      return;
    }
    this.terminating.set(true);
    try {
      const updated = await this.api.changeStatus(e.id, 'Terminated');
      this.employee.set(updated);
      this.statusDraft.set(updated.status);
      this.terminateOpen.set(false);
      this.toast.show(this.i18n.translate('employees.terminated'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('employees.terminateFailed'), 'danger');
    } finally {
      this.terminating.set(false);
    }
  }

  async saveStatus(): Promise<void> {
    const e = this.employee();
    if (!e || !this.statusDraft()) return;
    this.statusSaving.set(true);
    try {
      const updated = await this.api.changeStatus(e.id, this.statusDraft());
      this.employee.set(updated);
      this.toast.show(this.i18n.translate('employees.statusChanged'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('employees.saveError'), 'danger');
    } finally {
      this.statusSaving.set(false);
    }
  }

  async submitUpload(): Promise<void> {
    const e = this.employee();
    const file = this.uploadFile();
    if (!e || !file || !this.uploadTypeId()) {
      this.uploadError.set(this.i18n.translate('employees.validationRequired'));
      return;
    }
    if (this.selectedType()?.hasExpiry && !this.uploadExpires()) {
      this.uploadError.set(this.i18n.translate('employees.expiryRequired'));
      return;
    }
    this.uploading.set(true);
    this.uploadError.set(null);
    try {
      const form = new FormData();
      form.append('documentTypeId', this.uploadTypeId());
      if (this.uploadRef()) form.append('referenceNumber', this.uploadRef());
      if (this.uploadIssued()) form.append('issuedOn', this.uploadIssued());
      if (this.uploadExpires()) form.append('expiresOn', this.uploadExpires());
      form.append('file', file, file.name);
      await this.api.uploadDocument(e.id, form);
      this.uploadOpen.set(false);
      this.toast.show(this.i18n.translate('employees.docUploaded'), 'success');
      this.docs.set(await this.api.listDocuments(e.id));
    } catch {
      this.uploadError.set(this.i18n.translate('employees.saveError'));
    } finally {
      this.uploading.set(false);
    }
  }

  async confirmDelete(): Promise<void> {
    const e = this.employee();
    const d = this.deleteTarget();
    if (!e || !d) return;
    this.deleting.set(true);
    try {
      await this.api.deleteDocument(e.id, d.id);
      this.deleteTarget.set(null);
      this.docs.set(await this.api.listDocuments(e.id));
      this.toast.show(this.i18n.translate('employees.docDeleted'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('employees.saveError'), 'danger');
    } finally {
      this.deleting.set(false);
    }
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.state.set('error');
      return;
    }
    try {
      const [e, docs, lookups] = await Promise.all([
        this.api.get(id),
        this.api.listDocuments(id),
        this.api.orgLookups(),
      ]);
      this.employee.set(e);
      this.docs.set(docs);
      this.docTypes.set(lookups.documentTypes);
      this.statusDraft.set(e.status);
      this.state.set('ready');
      if (this.canManageAttendance()) {
        this.policyLoadError.set(null);
        try {
          this.attendancePolicy.set(await this.attendanceApi.getEmployeeAttendancePolicy(id));
        } catch {
          this.attendancePolicy.set(null);
          this.policyLoadError.set(this.i18n.translate('employees.attendancePolicyFailed'));
        }
      } else {
        this.attendancePolicy.set(null);
      }
    } catch (err: unknown) {
      const code = (err as { error?: { code?: string } })?.error?.code;
      this.state.set(code === 'plan.feature_not_available' ? 'locked' : 'error');
    }
  }
}
