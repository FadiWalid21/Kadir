import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';
import { PermissionCodes } from '../../core/auth/permission-codes';

@Component({
  selector: 'kadr-attendance-subnav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, TranslocoPipe],
  template: `
    <nav
      class="mb-6 flex flex-wrap gap-1 border-b border-line pb-px"
      [attr.aria-label]="'attendance.navAria' | transloco"
    >
      <a
        routerLink="/attendance/me"
        routerLinkActive="border-accent text-fg"
        class="border-b-2 border-transparent px-3 py-2 text-sm font-medium text-fg-2 hover:text-fg"
        [class.!border-accent]="active() === 'me'"
        [class.!text-fg]="active() === 'me'"
      >
        {{ 'attendance.nav.myLog' | transloco }}
      </a>
      @if (showSettings()) {
        <a
          routerLink="/attendance/settings/rules"
          routerLinkActive="border-accent text-fg"
          class="border-b-2 border-transparent px-3 py-2 text-sm font-medium text-fg-2 hover:text-fg"
          [class.!border-accent]="active() === 'settings'"
          [class.!text-fg]="active() === 'settings'"
        >
          {{ 'attendance.nav.settings' | transloco }}
        </a>
      }
    </nav>
  `,
})
export class AttendanceSubnavComponent {
  private readonly auth = inject(AuthService);

  readonly active = input<'me' | 'settings'>('me');

  protected readonly showSettings = computed(() =>
    this.auth.hasPermission(PermissionCodes.AttendanceManage),
  );
}
