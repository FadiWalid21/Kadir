import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { problemDetailsFromError } from '../../core/api/auth.api';
import { InvitationListItem } from '../../core/api/users.api';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';
import { DirtyFormRegistry } from '../../core/forms/dirty-form.registry';
import { SubscriptionStore } from '../../core/subscription/subscription.store';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { DrawerComponent } from '../../shared/ui/drawer/drawer.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { InputComponent } from '../../shared/ui/input/input.component';
import { RadioCardComponent } from '../../shared/ui/radio-card/radio-card.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { UsersStore } from './users.store';

@Component({
  selector: 'kadr-invitations-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [UsersStore],
  imports: [
    TranslocoPipe,
    ButtonComponent,
    InputComponent,
    RadioCardComponent,
    BadgeComponent,
    SkeletonComponent,
    EmptyStateComponent,
    DrawerComponent,
    ConfirmDialogComponent,
    IconComponent,
  ],
  template: `
    <div class="mx-auto flex w-full max-w-6xl flex-col gap-5">
      <header class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div class="min-w-0 flex-1 text-start">
          <h1 class="text-2xl font-bold leading-tight text-fg">
            {{ 'settings.invitations.title' | transloco }}
          </h1>
          <p class="mt-1 max-w-2xl text-sm text-fg-2">
            {{ 'settings.invitations.subtitle' | transloco }}
          </p>
        </div>
        @if (canInvitePerm()) {
          <span
            class="inline-flex shrink-0"
            [attr.title]="readOnly() ? ('subscription.readOnlyHint' | transloco) : null"
          >
            <kadr-button
              variant="primary"
              type="button"
              [disabled]="readOnly()"
              (click)="openInvite()"
            >
              <span class="inline-flex items-center gap-2">
                @if (readOnly()) {
                  <kadr-icon name="lock" size="sm" />
                } @else {
                  <kadr-icon name="plus-square" size="sm" />
                }
                {{ 'settings.users.invite' | transloco }}
              </span>
            </kadr-button>
          </span>
        }
      </header>

      @if (store.invitesState() === 'loading') {
        <kadr-skeleton height="8rem" />
      } @else if (store.invitesState() === 'error') {
        <kadr-empty-state
          [title]="'settings.users.invitesErrorTitle' | transloco"
          [description]="'settings.users.loadErrorBody' | transloco"
        >
          <kadr-button variant="secondary" type="button" (click)="store.loadInvitations()">
            {{ 'home.retry' | transloco }}
          </kadr-button>
        </kadr-empty-state>
      } @else if (store.invitations().length === 0) {
        <kadr-empty-state
          [title]="'settings.users.invitesEmptyTitle' | transloco"
          [description]="'settings.users.invitesEmptyBody' | transloco"
        />
      } @else {
        <ul class="flex flex-col gap-2">
          @for (inv of store.invitations(); track inv.id) {
            <li
              class="flex flex-col gap-2 rounded-md border border-line bg-surface p-3 sm:flex-row sm:items-center sm:justify-between"
            >
              <div class="min-w-0 text-start">
                <p class="truncate font-medium text-fg" dir="ltr">{{ inv.email }}</p>
                <div class="mt-1 flex flex-wrap items-center gap-2">
                  <kadr-badge tone="warning" [label]="'settings.users.pending' | transloco" />
                  <kadr-badge tone="neutral" [label]="inv.roleName" />
                </div>
              </div>
              @if (canInvite()) {
                <div class="flex flex-wrap gap-2">
                  <kadr-button
                    variant="secondary"
                    size="sm"
                    type="button"
                    [loading]="store.busyInviteId() === inv.id"
                    [disabled]="rowActionsLocked(inv.id)"
                    (click)="resend(inv)"
                  >
                    {{ 'settings.users.resend' | transloco }}
                  </kadr-button>
                  <kadr-button
                    variant="danger-soft"
                    size="sm"
                    type="button"
                    [disabled]="rowActionsLocked(inv.id)"
                    (click)="askRevoke(inv)"
                  >
                    {{ 'settings.users.revoke' | transloco }}
                  </kadr-button>
                </div>
              }
            </li>
          }
        </ul>
      }

      <kadr-drawer
        [open]="inviteOpen()"
        size="lg"
        [title]="'settings.users.inviteTitle' | transloco"
        [subtitle]="'settings.users.inviteSubtitle' | transloco"
        [closeLabel]="'ui.cancel' | transloco"
        (closed)="inviteOpen.set(false)"
      >
        <kadr-input
          [label]="'settings.users.inviteEmailLabel' | transloco"
          type="email"
          [required]="true"
          [value]="inviteEmail()"
          [error]="inviteError() ?? undefined"
          [hint]="'settings.users.inviteEmailHint' | transloco"
          (valueChange)="inviteEmail.set($event); inviteError.set(null)"
        />
        <p class="mb-2 mt-5 text-sm font-medium text-fg">
          {{ 'settings.users.inviteRole' | transloco }}
          <span class="text-danger" aria-hidden="true"> *</span>
        </p>
        <div
          class="flex flex-col gap-2"
          role="radiogroup"
          [attr.aria-label]="'settings.users.inviteRole' | transloco"
        >
          @for (role of store.roles(); track role.id) {
            <kadr-radio-card
              [title]="role.name"
              [description]="roleDescription(role.descriptionAr, role.descriptionEn)"
              [selected]="inviteRoleId() === role.id"
              [disabled]="role.name === 'Owner'"
              [disabledNote]="
                role.name === 'Owner' ? ('settings.users.ownerCannotGrant' | transloco) : undefined
              "
              (selectedChange)="inviteRoleId.set(role.id)"
            />
          }
        </div>
        <div
          class="mt-5 flex items-start gap-2 rounded-md border border-line bg-info-soft px-3 py-2.5 text-start"
          role="note"
        >
          <kadr-icon name="info" size="sm" class="mt-0.5 shrink-0 text-info" />
          <p class="text-xs text-fg-2">{{ 'settings.users.inviteExpiryNote' | transloco }}</p>
        </div>
        <div drawerFooter class="contents">
          <kadr-button
            variant="primary"
            type="button"
            [loading]="store.busy()"
            (click)="submitInvite()"
          >
            {{ 'settings.users.sendInvite' | transloco }}
          </kadr-button>
          <kadr-button variant="secondary" type="button" (click)="inviteOpen.set(false)">
            {{ 'ui.cancel' | transloco }}
          </kadr-button>
        </div>
      </kadr-drawer>

      <kadr-confirm-dialog
        [open]="!!revokeTarget()"
        [title]="'settings.users.revokeTitle' | transloco"
        [message]="'settings.users.revokeBody' | transloco"
        [confirmLabel]="'settings.users.revoke' | transloco"
        [cancelLabel]="'ui.cancel' | transloco"
        [danger]="true"
        [busy]="!!revokeTarget() && store.busyInviteId() === revokeTarget()!.id"
        (confirmed)="confirmRevoke()"
        (cancelled)="revokeTarget.set(null)"
      />
    </div>
  `,
})
export class InvitationsSettingsPage implements OnInit {
  protected readonly store = inject(UsersStore);
  private readonly auth = inject(AuthService);
  private readonly subscription = inject(SubscriptionStore);
  private readonly toast = inject(ToastService);
  private readonly i18n = inject(TranslocoService);
  private readonly dirtyForms = inject(DirtyFormRegistry);

  protected readonly inviteOpen = signal(false);
  protected readonly inviteEmail = signal('');
  protected readonly inviteRoleId = signal('');
  protected readonly inviteError = signal<string | null>(null);
  protected readonly revokeTarget = signal<InvitationListItem | null>(null);

  protected readonly readOnly = computed(() => this.subscription.readOnly());
  protected readonly canInvitePerm = computed(() =>
    this.auth.hasPermission(PermissionCodes.UsersInvite),
  );
  protected readonly canInvite = computed(() => this.canInvitePerm() && !this.readOnly());

  constructor() {
    effect(() => {
      const dirty = this.inviteOpen() && !!this.inviteEmail().trim();
      if (dirty) {
        this.dirtyForms.register('invitations-invite-drawer');
      } else {
        this.dirtyForms.clear('invitations-invite-drawer');
      }
    });
  }

  async ngOnInit(): Promise<void> {
    await this.store.loadRoles();
    await this.store.loadInvitations();
    const firstAssignable = this.store.roles().find((r) => r.name !== 'Owner');
    if (firstAssignable) {
      this.inviteRoleId.set(firstAssignable.id);
    }
  }

  protected roleDescription(ar: string | null, en: string | null): string | undefined {
    const lang = this.i18n.getActiveLang();
    return (lang === 'ar' ? ar : en) ?? ar ?? en ?? undefined;
  }

  /** True when another invitation row is mid resend/revoke. */
  protected rowActionsLocked(inviteId: string): boolean {
    const busyId = this.store.busyInviteId();
    return busyId !== null && busyId !== inviteId;
  }

  protected openInvite(): void {
    this.inviteEmail.set('');
    this.inviteError.set(null);
    this.inviteOpen.set(true);
  }

  protected async submitInvite(): Promise<void> {
    const email = this.inviteEmail().trim();
    if (!email || !this.inviteRoleId()) {
      this.inviteError.set(this.i18n.translate('ui.emailError'));
      return;
    }
    try {
      const created = await this.store.invite(email, this.inviteRoleId());
      this.inviteOpen.set(false);
      await this.notifyInviteLink(created.acceptUrl ?? null, 'settings.users.inviteSent');
    } catch (e) {
      const code = problemDetailsFromError(e as HttpErrorResponse)?.code;
      this.inviteError.set(
        code === 'auth.email_taken'
          ? this.i18n.translate('settings.users.emailTaken')
          : this.i18n.translate('settings.users.inviteFailed'),
      );
    }
  }

  protected async resend(inv: InvitationListItem): Promise<void> {
    try {
      const resent = await this.store.resend(inv.id);
      await this.notifyInviteLink(resent.acceptUrl ?? null, 'settings.users.resent');
    } catch {
      this.toast.show(this.i18n.translate('settings.users.inviteFailed'), 'danger');
    }
  }

  private async notifyInviteLink(acceptUrl: string | null, messageKey: string): Promise<void> {
    if (acceptUrl) {
      try {
        await navigator.clipboard.writeText(acceptUrl);
      } catch {
        /* clipboard may be denied; still show open link */
      }
      this.toast.show(this.i18n.translate(messageKey), 'success', 12_000, {
        label: this.i18n.translate('settings.users.copyInviteLink'),
        url: acceptUrl,
      });
      return;
    }

    this.toast.show(this.i18n.translate(messageKey), 'success');
  }

  protected askRevoke(inv: InvitationListItem): void {
    this.revokeTarget.set(inv);
  }

  protected async confirmRevoke(): Promise<void> {
    const inv = this.revokeTarget();
    if (!inv) {
      return;
    }
    try {
      await this.store.revoke(inv.id);
      this.revokeTarget.set(null);
      this.toast.show(this.i18n.translate('settings.users.revoked'), 'success');
    } catch {
      this.toast.show(this.i18n.translate('settings.users.inviteFailed'), 'danger');
    }
  }
}
