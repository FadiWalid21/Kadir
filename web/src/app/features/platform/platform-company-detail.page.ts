import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PlatformApi } from '../../core/api/platform.api';
import {
  PlatformPlan,
  PlatformTenantAuditSummary,
  PlatformTenantDetail,
} from '../../core/api/platform.models';
import { AvatarComponent } from '../../shared/ui/avatar/avatar.component';
import { BadgeComponent, BadgeTone } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { DropdownMenuComponent } from '../../shared/ui/dropdown-menu/dropdown-menu.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { DateFmtPipe } from './date-fmt.pipe';

type Tab = 'overview' | 'subscription' | 'audit';
type PageState = 'loading' | 'ready' | 'error';
type DrawerKind = 'plan' | 'extend' | null;
type DialogKind = 'suspend' | 'resume' | 'cancel' | 'industry' | null;
type AuditPeriod = '7' | '30';

@Component({
  selector: 'kadr-platform-company-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DrawerComponent,
    DateFmtPipe,
    AvatarComponent,
    IconComponent,
    DropdownMenuComponent,
    ConfirmDialogComponent,
  ],
  template: `
    @if (state() === 'loading') {
      <div class="flex flex-col gap-4">
        <kadr-skeleton height="2rem" width="40%" />
        <kadr-skeleton height="10rem" />
      </div>
    } @else if (state() === 'error' || !detail()) {
      <kadr-empty-state
        [title]="'platform.detail.loadErrorTitle' | transloco"
        [description]="'platform.detail.loadErrorBody' | transloco"
      >
        <kadr-button variant="secondary" type="button" (click)="reload()">
          {{ 'home.retry' | transloco }}
        </kadr-button>
      </kadr-empty-state>
    } @else {
      @let d = detail()!;
      <div class="flex flex-col gap-6">
        <nav class="text-sm text-fg-2" [attr.aria-label]="'platform.detail.breadcrumb' | transloco">
          <a routerLink="/platform/companies" class="text-accent hover:underline">
            {{ 'platform.detail.breadcrumb' | transloco }}
          </a>
          <span class="mx-1.5 text-fg-muted" aria-hidden="true">/</span>
          <span class="text-fg">{{ d.company.name }}</span>
        </nav>

        <section class="rounded-lg border border-line bg-surface p-5">
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div class="flex min-w-0 items-start gap-3">
              <kadr-avatar [name]="d.company.name" size="lg" [ariaLabel]="d.company.name" />
              <div class="min-w-0 text-start">
                <div class="flex flex-wrap items-center gap-2">
                  <h1 class="text-2xl font-bold text-fg">{{ d.company.name }}</h1>
                  <kadr-badge
                    [tone]="statusTone(d.company.status)"
                    [label]="statusLabel(d.company.status)"
                  />
                </div>
                <p class="mt-1 text-sm text-fg-2">
                  <bdi>{{ d.company.slug }}</bdi>
                  · {{ d.company.industryCode }}
                  · {{ d.company.timeZone }}
                </p>
              </div>
            </div>

            <kadr-dropdown-menu [ariaLabel]="'platform.detail.actionsMenuAria' | transloco">
              <span
                dropdownTrigger
                class="inline-flex h-10 items-center gap-2 rounded-md border border-line px-3 text-sm text-fg hover:bg-hover"
              >
                <kadr-icon name="settings" size="sm" />
                {{ 'platform.detail.actionsMenu' | transloco }}
                <kadr-icon name="chevron-down" size="sm" />
              </span>
              <div dropdownContent class="flex min-w-52 flex-col">
                <button
                  type="button"
                  class="rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
                  role="menuitem"
                  (click)="openDrawer('plan')"
                >
                  {{ 'platform.actions.changePlan' | transloco }}
                </button>
                <button
                  type="button"
                  class="rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
                  role="menuitem"
                  (click)="openDrawer('extend')"
                >
                  {{ 'platform.actions.extendTrial' | transloco }}
                </button>
                <button
                  type="button"
                  class="rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
                  role="menuitem"
                  (click)="openDialog('industry')"
                >
                  {{ 'platform.actions.changeIndustry' | transloco }}
                </button>
                @if (d.company.status === 'Suspended') {
                  <button
                    type="button"
                    class="rounded-sm px-3 py-2 text-start text-sm text-fg hover:bg-hover focus-visible:outline-none"
                    role="menuitem"
                    (click)="openDialog('resume')"
                  >
                    {{ 'platform.actions.resume' | transloco }}
                  </button>
                } @else {
                  <button
                    type="button"
                    class="rounded-sm px-3 py-2 text-start text-sm text-danger hover:bg-hover focus-visible:outline-none"
                    role="menuitem"
                    (click)="openDialog('suspend')"
                  >
                    {{ 'platform.actions.suspend' | transloco }}
                  </button>
                }
                <button
                  type="button"
                  class="rounded-sm px-3 py-2 text-start text-sm text-danger hover:bg-hover focus-visible:outline-none"
                  role="menuitem"
                  (click)="openDialog('cancel')"
                >
                  {{ 'platform.actions.cancelSub' | transloco }}
                </button>
              </div>
            </kadr-dropdown-menu>
          </div>

          <div class="mt-5 flex flex-wrap gap-1 border-b border-line" role="tablist">
            @for (t of tabs; track t) {
              <button
                type="button"
                role="tab"
                class="h-10 px-3 text-sm focus-visible:outline-none"
                [class.text-accent]="tab() === t"
                [class.border-b-2]="tab() === t"
                [class.border-accent]="tab() === t"
                [class.text-fg-2]="tab() !== t"
                (click)="selectTab(t)"
              >
                {{ ('platform.detail.tab.' + t) | transloco }}
                @if (t === 'subscription' && d.subscription?.daysLeft !== null && d.subscription?.daysLeft !== undefined) {
                  <span class="ms-1 rounded-full bg-subtle px-1.5 text-xs text-fg-2">
                    {{ d.subscription!.daysLeft }}
                  </span>
                }
              </button>
            }
          </div>
        </section>

        @if (tab() === 'overview') {
          <div class="grid gap-4 lg:grid-cols-2">
            <section class="rounded-lg border border-line bg-surface p-5">
              <h2 class="mb-3 text-base font-bold text-fg">{{ 'platform.detail.companyData' | transloco }}</h2>
              <dl class="flex flex-col gap-2 text-sm">
                <div class="flex justify-between gap-3">
                  <dt class="text-fg-2">{{ 'platform.companies.colCompany' | transloco }}</dt>
                  <dd class="text-end text-fg">{{ d.company.name }}</dd>
                </div>
                <div class="flex justify-between gap-3">
                  <dt class="text-fg-2">{{ 'platform.detail.slug' | transloco }}</dt>
                  <dd class="text-end text-fg"><bdi>{{ d.company.slug }}</bdi></dd>
                </div>
                <div class="flex justify-between gap-3">
                  <dt class="text-fg-2">{{ 'platform.detail.industry' | transloco }}</dt>
                  <dd class="text-end text-fg">
                    <span class="inline-flex items-center gap-1">
                      {{ d.company.industryCode }}
                      <kadr-icon name="lock" size="sm" class="text-fg-muted" />
                    </span>
                    <span class="mt-1 block text-xs text-fg-muted">{{
                      'platform.detail.industryLocked' | transloco
                    }}</span>
                  </dd>
                </div>
                <div class="flex justify-between gap-3">
                  <dt class="text-fg-2">{{ 'platform.detail.language' | transloco }}</dt>
                  <dd class="text-fg">{{ d.company.defaultLanguage }}</dd>
                </div>
                <div class="flex justify-between gap-3">
                  <dt class="text-fg-2">{{ 'platform.detail.timezone' | transloco }}</dt>
                  <dd class="text-fg">{{ d.company.timeZone }}</dd>
                </div>
                <div class="flex justify-between gap-3">
                  <dt class="text-fg-2">{{ 'platform.detail.created' | transloco }}</dt>
                  <dd class="tabular-nums text-fg">{{ d.company.createdAt | dateFmt }}</dd>
                </div>
                @if (d.owner) {
                  <div class="flex justify-between gap-3">
                    <dt class="text-fg-2">{{ 'platform.detail.owner' | transloco }}</dt>
                    <dd class="text-end text-fg">
                      {{ d.owner.fullName }}
                      <span class="block text-xs text-fg-muted"><bdi>{{ d.owner.email }}</bdi></span>
                    </dd>
                  </div>
                }
              </dl>
            </section>

            <section class="rounded-lg border border-line bg-surface p-5">
              <h2 class="mb-3 text-base font-bold text-fg">{{ 'platform.detail.usage' | transloco }}</h2>
              <ul class="flex flex-col gap-2 text-sm text-fg">
                <li class="flex justify-between gap-3">
                  <span class="text-fg-2">{{ 'platform.detail.users' | transloco }}</span>
                  <span class="tabular-nums">{{ d.usage.userCount }}</span>
                </li>
                <li class="flex justify-between gap-3">
                  <span class="text-fg-2">{{ 'platform.detail.employees' | transloco }}</span>
                  <span class="text-end tabular-nums">
                    {{ d.usage.activeEmployeeCount }}
                    @if (d.usage.employeeCap !== null) {
                      / {{ d.usage.employeeCap }}
                    }
                  </span>
                </li>
                @if (d.owner?.lastLoginAt) {
                  <li class="flex justify-between gap-3">
                    <span class="text-fg-2">{{ 'platform.detail.lastLogin' | transloco }}</span>
                    <span class="tabular-nums">{{ d.owner!.lastLoginAt | dateFmt }}</span>
                  </li>
                }
              </ul>
            </section>

            <section class="rounded-lg border border-line bg-surface p-5">
              <div class="mb-3 flex items-center justify-between gap-2">
                <h2 class="text-base font-bold text-fg">{{ 'platform.detail.currentSubscription' | transloco }}</h2>
                @if (d.subscription) {
                  <kadr-badge
                    [tone]="statusTone(d.company.status)"
                    [label]="statusLabel(d.company.status)"
                  />
                }
              </div>
              @if (d.subscription; as sub) {
                <p class="text-lg font-bold text-fg">
                  {{ i18n.getActiveLang() === 'ar' ? sub.planNameAr : sub.planNameEn }}
                </p>
                @if (sub.daysLeft !== null) {
                  <p class="mt-1 text-sm text-fg-2">
                    {{ 'platform.detail.daysLeft' | transloco: { days: sub.daysLeft } }}
                  </p>
                  <div class="mt-3 h-2 overflow-hidden rounded-full bg-subtle">
                    <div
                      class="h-full rounded-full bg-accent"
                      [style.width.%]="trialProgress(sub.daysLeft)"
                    ></div>
                  </div>
                }
                <div class="mt-4 flex flex-wrap gap-2">
                  <kadr-button variant="secondary" type="button" (click)="openDrawer('plan')">
                    {{ 'platform.actions.changePlan' | transloco }}
                  </kadr-button>
                  <kadr-button variant="secondary" type="button" (click)="openDrawer('extend')">
                    {{ 'platform.actions.extendTrial' | transloco }}
                  </kadr-button>
                </div>
              } @else {
                <p class="text-sm text-fg-2">{{ 'platform.detail.noSubscription' | transloco }}</p>
              }
            </section>

            <section class="rounded-lg border border-line bg-surface p-5">
              <h2 class="mb-3 text-base font-bold text-fg">{{ 'platform.detail.platformActions' | transloco }}</h2>
              @if (auditLoading() && !audit()) {
                <kadr-skeleton height="6rem" />
              } @else if (audit(); as a) {
                <ul class="flex flex-col gap-2 text-sm">
                  @for (e of a.platformActions.slice(0, 3); track e.id) {
                    <li class="border-b border-line py-2 text-fg">
                      <span class="text-fg-muted tabular-nums">{{ e.at | dateFmt }}</span>
                      · {{ e.action }}
                      @if (e.reason) {
                        <p class="text-xs text-fg-2">{{ e.reason }}</p>
                      }
                    </li>
                  } @empty {
                    <li class="text-fg-2">{{ 'platform.detail.noEvents' | transloco }}</li>
                  }
                </ul>
                <button
                  type="button"
                  class="mt-3 inline-flex items-center gap-1 text-sm text-accent hover:underline focus-visible:outline-none"
                  (click)="selectTab('audit')"
                >
                  {{ 'platform.detail.viewAuditTab' | transloco }}
                  <kadr-icon name="chevron-end" size="sm" [rtlFlip]="true" />
                </button>
              }
            </section>
          </div>
        }

        @if (tab() === 'subscription') {
          <div class="flex flex-col gap-4">
            @if (d.company.status === 'Suspended') {
              <div class="flex items-start gap-2 rounded-md bg-danger-soft px-3 py-3 text-sm text-danger">
                <kadr-icon name="lock" size="sm" />
                <div class="flex flex-1 flex-wrap items-center justify-between gap-2">
                  <p>{{ 'platform.detail.suspendedNote' | transloco }}</p>
                  <kadr-button variant="primary" type="button" (click)="openDialog('resume')">
                    {{ 'platform.actions.resume' | transloco }}
                  </kadr-button>
                </div>
              </div>
            }
            @if (isTrialExpired(d)) {
              <div class="flex items-start gap-2 rounded-md bg-warning-soft px-3 py-3 text-sm text-warning">
                <kadr-icon name="alert" size="sm" />
                <div class="flex flex-1 flex-wrap items-center justify-between gap-2">
                  <p>
                    <kadr-badge tone="neutral" [label]="'platform.status.expired' | transloco" />
                    <span class="ms-2">{{ 'platform.detail.expiredNote' | transloco }}</span>
                  </p>
                  <kadr-button variant="secondary" type="button" (click)="openDrawer('extend')">
                    {{ 'platform.actions.extendTrial' | transloco }}
                  </kadr-button>
                </div>
              </div>
            }

            <section class="rounded-lg border border-line bg-surface p-5">
              @if (d.subscription; as sub) {
                <div class="flex flex-wrap items-start justify-between gap-3">
                  <div class="text-start">
                    <kadr-badge
                      [tone]="statusTone(d.company.status)"
                      [label]="statusLabel(d.company.status)"
                    />
                    <p class="mt-2 text-lg font-bold text-fg">
                      {{ i18n.getActiveLang() === 'ar' ? sub.planNameAr : sub.planNameEn }}
                    </p>
                  </div>
                  <div class="flex flex-wrap gap-2">
                    <kadr-button variant="secondary" type="button" (click)="openDrawer('plan')">
                      {{ 'platform.actions.changePlan' | transloco }}
                    </kadr-button>
                    <kadr-button variant="secondary" type="button" (click)="openDrawer('extend')">
                      {{ 'platform.actions.extendTrial' | transloco }}
                    </kadr-button>
                  </div>
                </div>
                <div class="mt-5 grid gap-4 sm:grid-cols-3">
                  <div>
                    <p class="text-xs text-fg-muted">{{ 'platform.detail.trialWindow' | transloco }}</p>
                    <p class="mt-1 text-sm text-fg">
                      {{ sub.startsAt | dateFmt }}
                      @if (sub.trialEndsAt) {
                        — {{ sub.trialEndsAt | dateFmt }}
                      }
                    </p>
                  </div>
                  <div>
                    <p class="text-xs text-fg-muted">{{ 'platform.detail.daysLeftLabel' | transloco }}</p>
                    @if (sub.daysLeft !== null) {
                      <p class="mt-1 text-sm tabular-nums text-fg">{{ sub.daysLeft }}</p>
                      <div class="mt-2 h-2 overflow-hidden rounded-full bg-subtle">
                        <div
                          class="h-full rounded-full bg-accent"
                          [style.width.%]="trialProgress(sub.daysLeft)"
                        ></div>
                      </div>
                    } @else {
                      <p class="mt-1 text-sm text-fg">—</p>
                    }
                  </div>
                  <div>
                    <p class="text-xs text-fg-muted">{{ 'platform.detail.employees' | transloco }}</p>
                    <p class="mt-1 text-sm tabular-nums text-fg">
                      {{ d.usage.activeEmployeeCount }}
                      @if (sub.employeeCap !== null) {
                        / {{ sub.employeeCap }}
                      }
                    </p>
                    <div class="mt-2 h-2 overflow-hidden rounded-full bg-subtle">
                      <div
                        class="h-full rounded-full bg-accent"
                        [style.width.%]="employeeUsagePct(d.usage.activeEmployeeCount, sub.employeeCap)"
                      ></div>
                    </div>
                  </div>
                </div>
              } @else {
                <p class="text-sm text-fg-2">{{ 'platform.detail.noSubscription' | transloco }}</p>
              }
            </section>

            <section class="rounded-lg border border-line bg-surface p-5">
              <h2 class="mb-1 text-base font-bold text-fg">{{ 'platform.detail.subHistory' | transloco }}</h2>
              <p class="mb-3 text-xs text-fg-muted">{{ 'platform.detail.subHistoryNote' | transloco }}</p>
              @if (audit(); as a) {
                <ul class="flex flex-col gap-2 text-sm">
                  @for (e of a.platformActions; track e.id) {
                    <li class="border-b border-line py-2">
                      <div class="flex flex-wrap items-center gap-2">
                        <span class="tabular-nums text-fg-muted">{{ e.at | dateFmt }}</span>
                        <kadr-badge tone="neutral" [label]="'platform.detail.manualTag' | transloco" />
                        <span class="text-fg">{{ e.action }}</span>
                      </div>
                      @if (e.adminName || e.adminEmail) {
                        <p class="text-xs text-fg-2">{{ e.adminName || e.adminEmail }}</p>
                      }
                      @if (e.reason) {
                        <p class="text-xs text-fg-2">{{ e.reason }}</p>
                      }
                    </li>
                  } @empty {
                    <li class="text-fg-2">{{ 'platform.detail.noEvents' | transloco }}</li>
                  }
                </ul>
              }
            </section>
          </div>
        }

        @if (tab() === 'audit') {
          @if (auditLoading() && !audit()) {
            <kadr-skeleton height="12rem" />
          } @else if (audit(); as a) {
            <div class="grid gap-4 lg:grid-cols-3">
              <section class="rounded-lg border border-line bg-surface p-4 lg:col-span-1">
                <div class="mb-3 flex flex-col gap-2">
                  <h2 class="text-sm font-bold text-fg">{{ 'platform.detail.categories' | transloco }}</h2>
                  <div
                    class="inline-flex rounded-md border border-line bg-subtle p-0.5"
                    role="group"
                    [attr.aria-label]="'platform.detail.categories' | transloco"
                  >
                    <button
                      type="button"
                      class="h-8 rounded-sm px-3 text-xs focus-visible:outline-none"
                      [class.bg-surface]="auditPeriod() === '7'"
                      [class.text-fg]="auditPeriod() === '7'"
                      [class.text-fg-2]="auditPeriod() !== '7'"
                      (click)="auditPeriod.set('7')"
                    >
                      {{ 'platform.detail.period7' | transloco }}
                    </button>
                    <button
                      type="button"
                      class="h-8 rounded-sm px-3 text-xs focus-visible:outline-none"
                      [class.bg-surface]="auditPeriod() === '30'"
                      [class.text-fg]="auditPeriod() === '30'"
                      [class.text-fg-2]="auditPeriod() !== '30'"
                      (click)="auditPeriod.set('30')"
                    >
                      {{ 'platform.detail.period30' | transloco }}
                    </button>
                  </div>
                </div>
                <ul class="flex flex-col gap-3 text-sm">
                  @for (c of a.categories; track c.category) {
                    <li class="text-fg">
                      <div class="mb-1 flex justify-between gap-2">
                        <span>{{ c.category }}</span>
                        <span class="tabular-nums text-fg-2">
                          {{ auditPeriod() === '7' ? c.last7Days : c.last30Days }}
                        </span>
                      </div>
                      <div class="h-1.5 overflow-hidden rounded-full bg-subtle">
                        <div
                          class="h-full rounded-full bg-accent"
                          [style.width.%]="categoryBarWidth(a.categories, c)"
                        ></div>
                      </div>
                    </li>
                  }
                </ul>
              </section>

              <div class="flex flex-col gap-4 lg:col-span-2">
                <section class="overflow-x-auto rounded-lg border border-line bg-surface">
                  <div class="border-b border-line px-4 py-3">
                    <h2 class="text-sm font-bold text-fg">{{ 'platform.detail.recentEvents' | transloco }}</h2>
                  </div>
                  <table class="w-full text-start text-sm">
                    <thead class="bg-subtle text-xs text-fg-2">
                      <tr>
                        <th class="px-3 py-2 font-medium">{{ 'platform.detail.colWhen' | transloco }}</th>
                        <th class="px-3 py-2 font-medium">{{ 'platform.detail.colAction' | transloco }}</th>
                        <th class="px-3 py-2 font-medium">{{ 'platform.detail.colEntity' | transloco }}</th>
                        <th class="px-3 py-2 font-medium">{{ 'platform.detail.colUser' | transloco }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (e of a.recentTenantEvents; track e.id) {
                        <tr
                          class="border-t border-line"
                          [class.bg-warning-soft]="isFailedLogin(e.action)"
                        >
                          <td class="px-3 py-2 tabular-nums text-fg-2">{{ e.at | dateFmt }}</td>
                          <td class="px-3 py-2 text-fg">
                            <span class="inline-flex items-center gap-1">
                              @if (isFailedLogin(e.action)) {
                                <kadr-icon name="alert" size="sm" class="text-warning" />
                                <kadr-badge
                                  tone="warning"
                                  [label]="'platform.detail.failedLogin' | transloco"
                                />
                              }
                              {{ e.action }}
                            </span>
                          </td>
                          <td class="px-3 py-2 text-fg-2">{{ e.entityType }}</td>
                          <td class="px-3 py-2 text-fg">{{ e.userName || '—' }}</td>
                        </tr>
                      } @empty {
                        <tr>
                          <td colspan="4" class="px-3 py-4 text-fg-2">
                            {{ 'platform.detail.noEvents' | transloco }}
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                  <p class="flex items-start gap-2 border-t border-line px-4 py-3 text-xs text-fg-muted">
                    <kadr-icon name="lock" size="sm" />
                    {{ 'platform.detail.auditPrivacy' | transloco }}
                  </p>
                </section>

                <section class="overflow-x-auto rounded-lg border border-line bg-surface">
                  <div class="border-b border-line px-4 py-3">
                    <h2 class="text-sm font-bold text-fg">{{ 'platform.detail.platformActions' | transloco }}</h2>
                  </div>
                  <table class="w-full text-start text-sm">
                    <thead class="bg-subtle text-xs text-fg-2">
                      <tr>
                        <th class="px-3 py-2 font-medium">{{ 'platform.detail.colWhen' | transloco }}</th>
                        <th class="px-3 py-2 font-medium">{{ 'platform.detail.colAdmin' | transloco }}</th>
                        <th class="px-3 py-2 font-medium">{{ 'platform.detail.colAction' | transloco }}</th>
                        <th class="px-3 py-2 font-medium">{{ 'platform.detail.colReason' | transloco }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (e of a.platformActions; track e.id) {
                        <tr class="border-t border-line">
                          <td class="px-3 py-2 tabular-nums text-fg-2">{{ e.at | dateFmt }}</td>
                          <td class="px-3 py-2 text-fg">{{ e.adminName || e.adminEmail || '—' }}</td>
                          <td class="px-3 py-2 text-fg">{{ e.action }}</td>
                          <td class="px-3 py-2 text-fg-2">{{ e.reason || '—' }}</td>
                        </tr>
                      } @empty {
                        <tr>
                          <td colspan="4" class="px-3 py-4 text-fg-2">
                            {{ 'platform.detail.noEvents' | transloco }}
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </section>
              </div>
            </div>
          }
        }
      </div>

      <kadr-drawer
        [open]="!!drawer()"
        [title]="drawerTitle() | transloco"
        [closeLabel]="'ui.close' | transloco"
        (closed)="closeDrawer()"
      >
        <div class="flex flex-col gap-4">
          @if (drawer() === 'plan') {
            <p class="text-sm text-fg-2">
              {{ 'platform.actions.currentPlan' | transloco }}:
              <bdi>{{ detail()?.subscription?.planCode || '—' }}</bdi>
            </p>
            <p class="text-xs text-fg-muted">{{ 'platform.actions.immediateNote' | transloco }}</p>
            <fieldset class="flex flex-col gap-2">
              <legend class="text-sm font-medium text-fg">{{ 'platform.actions.plan' | transloco }}</legend>
              @for (p of seededPlans(); track p.id) {
                <label
                  class="flex cursor-pointer items-start gap-3 rounded-md border px-3 py-3"
                  [class.border-accent]="planCode() === p.code"
                  [class.bg-accent-soft]="planCode() === p.code"
                  [class.border-line]="planCode() !== p.code"
                  [class.opacity-50]="p.code === detail()?.subscription?.planCode"
                >
                  <input
                    type="radio"
                    class="mt-1 accent-accent"
                    name="planCode"
                    [value]="p.code"
                    [checked]="planCode() === p.code"
                    [disabled]="p.code === detail()?.subscription?.planCode"
                    (change)="planCode.set(p.code)"
                  />
                  <span class="text-sm text-fg">
                    {{ i18n.getActiveLang() === 'ar' ? p.nameAr : p.nameEn }}
                    (<bdi class="text-fg-muted">({{ p.code }})</bdi>)
                    @if (p.code === detail()?.subscription?.planCode) {
                      <kadr-badge
                        tone="neutral"
                        [label]="'platform.actions.currentTag' | transloco"
                      />
                    }
                  </span>
                </label>
              }
            </fieldset>
          }
          @if (drawer() === 'extend') {
            @if (isTrialExpired(detail()!)) {
              <p class="rounded-md bg-warning-soft px-3 py-2 text-sm text-warning">
                {{ 'platform.actions.extendExpiredNote' | transloco }}
              </p>
            }
            <div class="inline-flex rounded-md border border-line bg-subtle p-0.5">
              @for (day of [7, 14, 30]; track day) {
                <button
                  type="button"
                  class="h-9 rounded-sm px-3 text-sm focus-visible:outline-none"
                  [class.bg-surface]="extendDays() === day"
                  [class.text-fg]="extendDays() === day"
                  [class.text-fg-2]="extendDays() !== day"
                  (click)="extendDays.set(day)"
                >
                  +{{ day }}
                </button>
              }
            </div>
            <p class="text-sm text-fg-2">
              {{ 'platform.actions.newEndDate' | transloco }}:
              {{ computedExtendDate() }}
            </p>
          }
          <label class="grid gap-1 text-sm text-fg">
            <span>{{ 'platform.actions.reason' | transloco }}</span>
            <textarea
              class="min-h-24 rounded-md border border-line-strong bg-surface px-3 py-2 text-sm"
              [ngModel]="reason()"
              (ngModelChange)="reason.set($event)"
            ></textarea>
          </label>
        </div>
        <div drawerFooter class="contents">
          <kadr-button variant="primary" type="button" [loading]="busy()" (click)="submitDrawer()">
            {{ drawerTitle() | transloco }}
          </kadr-button>
          <kadr-button variant="secondary" type="button" (click)="closeDrawer()">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
        </div>
      </kadr-drawer>

      <kadr-confirm-dialog
        [open]="!!dialog()"
        [title]="dialogTitle() | transloco"
        [message]="dialogMessage()"
        [confirmLabel]="dialogConfirmLabel() | transloco"
        [cancelLabel]="'ui.cancel' | transloco"
        [danger]="dialog() === 'suspend' || dialog() === 'cancel'"
        [busy]="busy()"
        (confirmed)="submitDialog()"
        (cancelled)="closeDialog()"
      >
        @if (dialog() === 'industry') {
          <div class="flex flex-col gap-3">
            <p class="rounded-md bg-warning-soft px-3 py-2 text-sm text-warning">
              {{ 'platform.actions.industryNote' | transloco }}
            </p>
            <p class="text-sm text-fg-2">
              {{ 'platform.actions.currentIndustry' | transloco }}:
              {{ detail()?.company.industryCode }}
            </p>
            <label class="grid gap-1 text-sm text-fg">
              <span>{{ 'platform.actions.newIndustry' | transloco }}</span>
              <select
                class="h-control rounded-md border border-line-strong bg-surface px-3"
                [ngModel]="industryCode()"
                (ngModelChange)="industryCode.set($event)"
              >
                @for (code of industries; track code) {
                  <option [value]="code">{{ code }}</option>
                }
              </select>
            </label>
            <label class="grid gap-1 text-sm text-fg">
              <span>{{ 'platform.actions.mode' | transloco }}</span>
              <select
                class="h-control rounded-md border border-line-strong bg-surface px-3"
                [ngModel]="industryMode()"
                (ngModelChange)="industryMode.set($event)"
              >
                <option value="AddMissing">AddMissing</option>
                <option value="ReplaceUntouched">ReplaceUntouched</option>
              </select>
            </label>
          </div>
        }
        <label class="mt-3 grid gap-1 text-sm text-fg">
          <span>{{ 'platform.actions.reason' | transloco }}</span>
          <textarea
            class="min-h-24 rounded-md border border-line-strong bg-surface px-3 py-2 text-sm"
            [ngModel]="reason()"
            (ngModelChange)="reason.set($event)"
          ></textarea>
        </label>
      </kadr-confirm-dialog>
    }
  `,
})
export class PlatformCompanyDetailPage implements OnInit {
  private readonly api = inject(PlatformApi);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);
  protected readonly i18n = inject(TranslocoService);

  protected readonly state = signal<PageState>('loading');
  protected readonly detail = signal<PlatformTenantDetail | null>(null);
  protected readonly audit = signal<PlatformTenantAuditSummary | null>(null);
  protected readonly auditLoading = signal(false);
  protected readonly auditPeriod = signal<AuditPeriod>('7');
  protected readonly tab = signal<Tab>('overview');
  protected readonly drawer = signal<DrawerKind>(null);
  protected readonly dialog = signal<DialogKind>(null);
  protected readonly plans = signal<PlatformPlan[]>([]);
  protected readonly planCode = signal('starter');
  protected readonly extendDays = signal(7);
  protected readonly industryCode = signal('office');
  protected readonly industryMode = signal('AddMissing');
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly tabs: Tab[] = ['overview', 'subscription', 'audit'];
  protected readonly industries = ['retail', 'clinic', 'factory', 'construction', 'office'];

  private tenantId = '';

  ngOnInit(): void {
    this.tenantId = this.route.snapshot.paramMap.get('id') ?? '';
    void this.reload();
  }

  reload(): void {
    void this.load();
  }

  selectTab(t: Tab): void {
    this.tab.set(t);
    if ((t === 'audit' || t === 'overview') && !this.audit()) {
      void this.loadAudit();
    }
  }

  openDrawer(kind: DrawerKind): void {
    this.reason.set('');
    this.drawer.set(kind);
    if (kind === 'plan' && this.plans().length === 0) {
      void this.api.listPlans().then((r) => {
        this.plans.set(r.items);
        const current = this.detail()?.subscription?.planCode;
        if (current) {
          this.planCode.set(current);
        }
      });
    }
  }

  closeDrawer(): void {
    this.drawer.set(null);
  }

  openDialog(kind: DialogKind): void {
    this.reason.set('');
    if (kind === 'industry') {
      this.industryCode.set(this.detail()?.company.industryCode ?? 'office');
    }
    this.dialog.set(kind);
  }

  closeDialog(): void {
    this.dialog.set(null);
  }

  drawerTitle(): string {
    return this.drawer() === 'extend'
      ? 'platform.actions.extendTrial'
      : 'platform.actions.changePlan';
  }

  dialogTitle(): string {
    switch (this.dialog()) {
      case 'suspend':
        return 'platform.actions.suspend';
      case 'resume':
        return 'platform.actions.resume';
      case 'cancel':
        return 'platform.actions.cancelSub';
      case 'industry':
        return 'platform.actions.changeIndustry';
      default:
        return 'ui.save';
    }
  }

  statusTone(status: string): BadgeTone {
    switch (status) {
      case 'Active':
        return 'success';
      case 'Trial':
        return 'info';
      case 'Suspended':
        return 'warning';
      default:
        return 'neutral';
    }
  }

  statusLabel(status: string): string {
    const key = `platform.status.${status.toLowerCase()}`;
    const t = this.i18n.translate(key);
    return t === key ? status : t;
  }

  trialProgress(daysLeft: number): number {
    return Math.max(4, Math.min(100, Math.round((daysLeft / 14) * 100)));
  }

  employeeUsagePct(active: number, cap: number | null): number {
    if (cap == null || cap <= 0) {
      return 0;
    }
    return Math.max(0, Math.min(100, Math.round((active / cap) * 100)));
  }

  isTrialExpired(d: PlatformTenantDetail): boolean {
    const sub = d.subscription;
    if (!sub?.trialEndsAt) {
      return false;
    }
    return new Date(sub.trialEndsAt).getTime() < Date.now() && d.company.status !== 'Suspended';
  }

  seededPlans(): PlatformPlan[] {
    return this.plans().filter((p) => p.code !== 'volume');
  }

  computedExtendDate(): string {
    const base = this.detail()?.subscription?.trialEndsAt;
    const start = base && new Date(base).getTime() > Date.now() ? new Date(base) : new Date();
    start.setUTCDate(start.getUTCDate() + this.extendDays());
    try {
      return new Intl.DateTimeFormat(this.i18n.getActiveLang() === 'ar' ? 'ar' : 'en', {
        dateStyle: 'medium',
      }).format(start);
    } catch {
      return start.toISOString();
    }
  }

  categoryBarWidth(
    cats: { category: string; last7Days: number; last30Days: number }[],
    c: { last7Days: number; last30Days: number },
  ): number {
    const values = cats.map((x) => (this.auditPeriod() === '7' ? x.last7Days : x.last30Days));
    const max = Math.max(1, ...values);
    const n = this.auditPeriod() === '7' ? c.last7Days : c.last30Days;
    return Math.round((n / max) * 100);
  }

  isFailedLogin(action: string): boolean {
    const a = action.toLowerCase();
    return a.includes('login.failed') || a.includes('failed_login') || a.includes('auth.login_failed');
  }

  dialogMessage(): string {
    switch (this.dialog()) {
      case 'suspend':
        return this.i18n.translate('platform.actions.suspendConsequence');
      case 'cancel':
        return this.i18n.translate('platform.actions.cancelConsequence');
      case 'resume':
        return this.i18n.translate('platform.actions.resumeConsequence');
      default:
        return '';
    }
  }

  dialogConfirmLabel(): string {
    switch (this.dialog()) {
      case 'suspend':
        return 'platform.actions.suspend';
      case 'resume':
        return 'platform.actions.resume';
      case 'cancel':
        return 'platform.actions.cancelSub';
      case 'industry':
        return 'platform.actions.changeIndustry';
      default:
        return 'platform.actions.confirm';
    }
  }

  async submitDrawer(): Promise<void> {
    if (!this.requireReason()) {
      return;
    }
    this.busy.set(true);
    try {
      const id = this.tenantId;
      const reason = this.reason().trim();
      if (this.drawer() === 'plan') {
        this.detail.set(await this.api.changePlan(id, this.planCode(), reason));
      } else if (this.drawer() === 'extend') {
        this.detail.set(await this.api.extendTrial(id, this.extendDays(), reason));
      }
      this.toast.show(this.i18n.translate('platform.actions.success'), 'success');
      this.closeDrawer();
      void this.loadAudit();
    } catch {
      this.toast.show(this.i18n.translate('platform.actions.failed'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  async submitDialog(): Promise<void> {
    if (!this.requireReason()) {
      return;
    }
    this.busy.set(true);
    try {
      const id = this.tenantId;
      const reason = this.reason().trim();
      switch (this.dialog()) {
        case 'suspend':
          this.detail.set(await this.api.suspend(id, reason));
          break;
        case 'resume':
          this.detail.set(await this.api.resume(id, reason));
          break;
        case 'cancel':
          this.detail.set(await this.api.cancelSubscription(id, reason));
          break;
        case 'industry':
          await this.api.changeIndustry(id, this.industryCode(), this.industryMode(), reason);
          await this.load();
          break;
      }
      this.toast.show(this.i18n.translate('platform.actions.success'), 'success');
      this.closeDialog();
      void this.loadAudit();
    } catch {
      this.toast.show(this.i18n.translate('platform.actions.failed'), 'danger');
    } finally {
      this.busy.set(false);
    }
  }

  private requireReason(): boolean {
    if (!this.reason().trim()) {
      this.toast.show(this.i18n.translate('platform.actions.reasonRequired'), 'danger');
      return false;
    }
    return true;
  }

  private async load(): Promise<void> {
    this.state.set('loading');
    try {
      this.detail.set(await this.api.getTenant(this.tenantId));
      this.state.set('ready');
      void this.loadAudit();
    } catch {
      this.state.set('error');
    }
  }

  private async loadAudit(): Promise<void> {
    this.auditLoading.set(true);
    try {
      this.audit.set(await this.api.auditSummary(this.tenantId));
    } catch {
      /* overview can still render without audit */
    } finally {
      this.auditLoading.set(false);
    }
  }
}
