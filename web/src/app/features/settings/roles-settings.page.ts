import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { CheckboxComponent } from '../../shared/ui/checkbox/checkbox.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { RolesStore } from './roles.store';

@Component({
  selector: 'kadr-roles-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [RolesStore],
  imports: [
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    CheckboxComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
    ConfirmDialogComponent,
    IconComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-5">

      <header class="text-start">
        <h1 class="text-2xl font-bold leading-tight text-fg">{{ 'settings.roles.title' | transloco }}</h1>
        <p class="mt-1 max-w-2xl text-sm text-fg-2">{{ 'settings.roles.subtitle' | transloco }}</p>
      </header>

      @if (store.listState() === 'loading') {
        <kadr-skeleton height="16rem" />
      } @else if (store.listState() === 'error') {
        <kadr-empty-state
          [title]="'settings.roles.loadErrorTitle' | transloco"
          [description]="'settings.roles.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="store.loadAll()">{{ 'home.retry' | transloco }}</kadr-button>
        </kadr-empty-state>
      } @else {
        <div class="grid gap-4 lg:grid-cols-[minmax(16rem,20rem)_minmax(0,1fr)] lg:items-stretch">
          <aside
            class="flex flex-col rounded-lg border border-line bg-surface"
            [attr.aria-label]="'settings.roles.listAria' | transloco"
          >
            <div class="flex flex-col gap-3 border-b border-line p-3">
              <kadr-input
                [label]="'settings.roles.searchRoles' | transloco"
                [placeholder]="'settings.roles.searchRoles' | transloco"
                [value]="roleQuery()"
                (valueChange)="roleQuery.set($event)"
              />
              @if (canManagePerm()) {
                <span
                  class="inline-flex"
                  [attr.title]="readOnly() ? ('subscription.readOnlyHint' | transloco) : null"
                >
                  <kadr-button
                    variant="primary"
                    type="button"
                    [disabled]="readOnly()"
                    (click)="startCreate()"
                  >
                    <span class="inline-flex items-center gap-2">
                      @if (readOnly()) {
                        <kadr-icon name="lock" size="sm" />
                      } @else {
                        <svg viewBox="0 0 24 24" class="size-4" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                          <path d="M12 5v14" />
                          <path d="M5 12h14" />
                        </svg>
                      }
                      {{ 'settings.roles.newRole' | transloco }}
                    </span>
                  </kadr-button>
                </span>
              }
            </div>

            <div class="min-h-0 flex-1 overflow-y-auto p-2">
              @if (filteredSystemRoles().length > 0) {
                <p class="px-2 pb-1 pt-2 text-xs font-medium text-fg-muted">
                  {{ 'settings.roles.systemGroup' | transloco }}
                </p>
                <ul class="mb-3 flex flex-col gap-1">
                  @for (role of filteredSystemRoles(); track role.id) {
                    <li>
                      <button
                        type="button"
                        class="flex w-full items-start gap-2 rounded-md px-3 py-2.5 text-start text-sm hover:bg-hover"
                        [class.bg-accent-soft]="store.selected()?.id === role.id && !creating()"
                        (click)="select(role.id)"
                      >
                        <span class="min-w-0 flex-1">
                          <span class="flex items-center gap-1.5">
                            <svg viewBox="0 0 24 24" class="size-3.5 shrink-0 text-fg-muted" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                              <rect x="5" y="11" width="14" height="10" rx="2" />
                              <path d="M8 11V8a4 4 0 0 1 8 0v3" />
                            </svg>
                            <span class="truncate font-medium text-fg">{{ role.name }}</span>
                          </span>
                          <span class="mt-0.5 block text-xs text-fg-muted">
                            {{ 'settings.roles.linkedUsers' | transloco: { count: role.userCount } }}
                          </span>
                        </span>
                        <kadr-badge tone="info" [label]="'settings.roles.system' | transloco" />
                      </button>
                    </li>
                  }
                </ul>
              }

              @if (filteredCustomRoles().length > 0) {
                <p class="px-2 pb-1 pt-1 text-xs font-medium text-fg-muted">
                  {{ 'settings.roles.customGroup' | transloco }}
                </p>
                <ul class="flex flex-col gap-1">
                  @for (role of filteredCustomRoles(); track role.id) {
                    <li>
                      <button
                        type="button"
                        class="flex w-full items-start gap-2 rounded-md px-3 py-2.5 text-start text-sm hover:bg-hover"
                        [class.bg-accent-soft]="store.selected()?.id === role.id && !creating()"
                        (click)="select(role.id)"
                      >
                        <span class="min-w-0 flex-1">
                          <span class="truncate font-medium text-fg">{{ role.name }}</span>
                          <span class="mt-0.5 block text-xs text-fg-muted">
                            {{ 'settings.roles.linkedUsers' | transloco: { count: role.userCount } }}
                          </span>
                        </span>
                        <kadr-badge tone="accent" [label]="'settings.roles.custom' | transloco" />
                      </button>
                    </li>
                  }
                </ul>
              }

              @if (filteredSystemRoles().length === 0 && filteredCustomRoles().length === 0) {
                <p class="px-2 py-4 text-sm text-fg-2">{{ 'settings.roles.loadErrorBody' | transloco }}</p>
              }
            </div>
          </aside>

          <section class="flex min-h-96 flex-col rounded-lg border border-line bg-surface">
            @if (creating()) {
              <div class="border-b border-line p-4 text-start">
                <h2 class="text-lg font-bold text-fg">{{ 'settings.roles.newRole' | transloco }}</h2>
                <div class="mt-3 grid gap-3">
                  <kadr-input [label]="'settings.roles.name' | transloco" [value]="draftName()" (valueChange)="draftName.set($event)" />
                  <kadr-input [label]="'settings.roles.descAr' | transloco" [value]="draftDescAr()" (valueChange)="draftDescAr.set($event)" />
                  <kadr-input [label]="'settings.roles.descEn' | transloco" [value]="draftDescEn()" (valueChange)="draftDescEn.set($event)" />
                </div>
              </div>
              <div class="min-h-0 flex-1 overflow-y-auto p-4">
                @for (group of store.permissionGroups(); track group.resource) {
                  <details class="mb-3 rounded-md border border-line" open>
                    <summary class="cursor-pointer px-3 py-2 text-sm font-medium text-fg">
                      {{ group.resource }}
                      <span class="ms-2 text-xs text-fg-muted">
                        {{ 'settings.roles.groupCount' | transloco: { on: selectedInGroup(group.resource), total: group.permissions.length } }}
                      </span>
                    </summary>
                    <div class="flex flex-col gap-3 border-t border-line px-3 py-3">
                      @for (perm of group.permissions; track perm.code) {
                        <kadr-checkbox
                          [label]="perm.code"
                          [description]="perm.labelKey"
                          [checked]="draftPerms().has(perm.code)"
                          [disabled]="!canGrant(perm.code)"
                          (checkedChange)="togglePerm(perm.code, $event)"
                        />
                      }
                    </div>
                  </details>
                }
              </div>
              <footer class="sticky bottom-0 flex flex-wrap gap-2 border-t border-line bg-surface px-4 py-3">
                <kadr-button variant="primary" type="button" [loading]="store.busy()" (click)="saveCreate()">{{ 'ui.save' | transloco }}</kadr-button>
                <kadr-button variant="secondary" type="button" (click)="creating.set(false)">{{ 'ui.cancel' | transloco }}</kadr-button>
              </footer>
            } @else if (store.detailState() === 'loading') {
              <div class="p-4"><kadr-skeleton height="12rem" /></div>
            } @else if (store.selected(); as role) {
              <div class="border-b border-line p-4 text-start">
                <div class="flex flex-wrap items-center gap-2">
                  @if (role.isSystem) {
                    <svg viewBox="0 0 24 24" class="size-4 text-fg-muted" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                      <rect x="5" y="11" width="14" height="10" rx="2" />
                      <path d="M8 11V8a4 4 0 0 1 8 0v3" />
                    </svg>
                  }
                  <h2 class="text-lg font-bold text-fg">{{ role.name }}</h2>
                  <kadr-badge
                    [tone]="role.isSystem ? 'info' : 'accent'"
                    [label]="(role.isSystem ? 'settings.roles.system' : 'settings.roles.custom') | transloco"
                  />
                  <span class="text-xs text-fg-muted">
                    {{ 'settings.roles.linkedUsers' | transloco: { count: role.userCount } }}
                  </span>
                </div>
                @if (roleDescription(role); as desc) {
                  <p class="mt-2 text-sm text-fg-2">{{ desc }}</p>
                }
                @if (role.isSystem) {
                  <p class="mt-3 rounded-md border border-line bg-subtle px-3 py-2 text-xs text-fg-2">
                    {{ 'settings.roles.systemReadonly' | transloco }}
                  </p>
                } @else if (canManage()) {
                  <div class="mt-3 grid gap-3">
                    <kadr-input [label]="'settings.roles.name' | transloco" [value]="draftName()" (valueChange)="draftName.set($event)" />
                    <kadr-input [label]="'settings.roles.descAr' | transloco" [value]="draftDescAr()" (valueChange)="draftDescAr.set($event)" />
                    <kadr-input [label]="'settings.roles.descEn' | transloco" [value]="draftDescEn()" (valueChange)="draftDescEn.set($event)" />
                  </div>
                }
              </div>
              <div class="min-h-0 flex-1 overflow-y-auto p-4">
                @for (group of store.permissionGroups(); track group.resource) {
                  <details class="mb-3 rounded-md border border-line" open>
                    <summary class="cursor-pointer px-3 py-2 text-sm font-medium text-fg">
                      {{ group.resource }}
                      <span class="ms-2 text-xs text-fg-muted">
                        {{ 'settings.roles.groupCount' | transloco: { on: selectedInGroup(group.resource), total: group.permissions.length } }}
                      </span>
                    </summary>
                    <div class="flex flex-col gap-3 border-t border-line px-3 py-3">
                      @for (perm of group.permissions; track perm.code) {
                        <kadr-checkbox
                          [label]="perm.code"
                          [description]="perm.labelKey"
                          [checked]="draftPerms().has(perm.code)"
                          [disabled]="role.isSystem || !canManage() || !canGrant(perm.code)"
                          (checkedChange)="togglePerm(perm.code, $event)"
                        />
                      }
                    </div>
                  </details>
                }
              </div>
              @if (canManage() && !role.isSystem) {
                <footer class="sticky bottom-0 flex flex-wrap items-center justify-between gap-2 border-t border-line bg-surface px-4 py-3">
                  <div class="flex flex-wrap gap-2">
                    <kadr-button variant="primary" type="button" [loading]="store.busy()" (click)="saveUpdate()">{{ 'ui.save' | transloco }}</kadr-button>
                    <kadr-button variant="secondary" type="button" (click)="revertDraft()">{{ 'settings.roles.revert' | transloco }}</kadr-button>
                  </div>
                  <kadr-button variant="danger-soft" type="button" (click)="deleteOpen.set(true)">
                    <span class="inline-flex items-center gap-2">
                      <svg viewBox="0 0 24 24" class="size-4" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                        <path d="M3 6h18" />
                        <path d="M8 6V4h8v2" />
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" />
                      </svg>
                      {{ 'ui.delete' | transloco }}
                    </span>
                  </kadr-button>
                </footer>
              }
            }
          </section>
        </div>
      }

      <kadr-confirm-dialog
        [open]="deleteOpen()"
        [title]="'settings.roles.deleteTitle' | transloco"
        [message]="'settings.roles.deleteBody' | transloco"
        [confirmLabel]="'ui.delete' | transloco"
        [cancelLabel]="'ui.cancel' | transloco"
        [danger]="true"
        [busy]="store.busy()"
        (confirmed)="confirmDelete()"
        (cancelled)="deleteOpen.set(false)"
      />
    </div>
  `,
})
export class RolesSettingsPage implements OnInit {
  protected readonly store = inject(RolesStore);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(TranslocoService);

  protected readonly creating = signal(false);
  protected readonly deleteOpen = signal(false);
  protected readonly roleQuery = signal('');
  protected readonly draftName = signal('');
  protected readonly draftDescAr = signal('');
  protected readonly draftDescEn = signal('');
  protected readonly draftPerms = signal(new Set<string>());

  protected readonly readOnly = computed(() => this.subscription.readOnly());
  protected readonly canManagePerm = computed(() =>
    this.auth.hasPermission(PermissionCodes.RolesManage),
  );
  protected readonly canManage = computed(() => this.canManagePerm() && !this.readOnly());

  private readonly myPermissions = computed(
    () => new Set(this.auth.me()?.permissions ?? []),
  );

  private readonly filteredRoles = computed(() => {
    const q = this.roleQuery().trim().toLowerCase();
    const roles = this.store.roles();
    if (!q) {
      return roles;
    }
    return roles.filter((r) => r.name.toLowerCase().includes(q));
  });

  protected readonly filteredSystemRoles = computed(() =>
    this.filteredRoles().filter((r) => r.isSystem),
  );

  protected readonly filteredCustomRoles = computed(() =>
    this.filteredRoles().filter((r) => !r.isSystem),
  );

  async ngOnInit(): Promise<void> {
    await this.store.loadAll();
    this.revertDraft();
  }

  protected canGrant(code: string): boolean {
    return this.myPermissions().has(code);
  }

  protected selectedInGroup(resource: string): number {
    const group = this.store.permissionGroups().find((g) => g.resource === resource);
    if (!group) {
      return 0;
    }
    return group.permissions.filter((p) => this.draftPerms().has(p.code)).length;
  }

  protected roleDescription(role: { descriptionAr: string | null; descriptionEn: string | null }): string | null {
    const lang = this.i18n.getActiveLang();
    const text = (lang === 'ar' ? role.descriptionAr : role.descriptionEn) ?? role.descriptionAr ?? role.descriptionEn;
    return text?.trim() ? text : null;
  }

  protected async select(id: string): Promise<void> {
    this.creating.set(false);
    await this.store.selectRole(id);
    this.revertDraft();
  }

  protected startCreate(): void {
    this.creating.set(true);
    this.draftName.set('');
    this.draftDescAr.set('');
    this.draftDescEn.set('');
    this.draftPerms.set(new Set());
  }

  protected revertDraft(): void {
    const role = this.store.selected();
    if (!role) {
      return;
    }
    this.draftName.set(role.name);
    this.draftDescAr.set(role.descriptionAr ?? '');
    this.draftDescEn.set(role.descriptionEn ?? '');
    this.draftPerms.set(new Set(role.permissions));
  }

  protected togglePerm(code: string, on: boolean): void {
    const next = new Set(this.draftPerms());
    if (on) {
      next.add(code);
    } else {
      next.delete(code);
    }
    this.draftPerms.set(next);
  }

  protected async saveCreate(): Promise<void> {
    try {
      await this.store.create({
        name: this.draftName().trim(),
        descriptionAr: this.draftDescAr().trim() || null,
        descriptionEn: this.draftDescEn().trim() || null,
        permissions: [...this.draftPerms()],
      });
      this.creating.set(false);
      this.toast.show(this.i18n.translate('settings.roles.saved'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.roles.saveFailed'), 'danger');
    }
  }

  protected async saveUpdate(): Promise<void> {
    const role = this.store.selected();
    if (!role) {
      return;
    }
    try {
      await this.store.update(role.id, {
        name: this.draftName().trim(),
        descriptionAr: this.draftDescAr().trim() || null,
        descriptionEn: this.draftDescEn().trim() || null,
        permissions: [...this.draftPerms()],
      });
      this.toast.show(this.i18n.translate('settings.roles.saved'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.roles.saveFailed'), 'danger');
    }
  }

  protected async confirmDelete(): Promise<void> {
    const role = this.store.selected();
    if (!role) {
      return;
    }
    try {
      await this.store.delete(role.id);
      this.deleteOpen.set(false);
      this.toast.show(this.i18n.translate('settings.roles.deleted'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.roles.deleteFailed'), 'danger');
    }
  }
}
