import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'kadr-attendance-settings-subnav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslocoPipe],
  template: `
    <nav
      class="mb-6 flex flex-wrap gap-1"
      [attr.aria-label]="'attendance.settings.navAria' | transloco"
    >
      <a
        routerLink="/attendance/settings/rules"
        class="rounded-md px-3 py-2 text-sm font-medium text-fg-2 hover:bg-hover"
        [class.bg-hover]="active() === 'rules'"
        [class.text-fg]="active() === 'rules'"
      >
        {{ 'attendance.settings.nav.rules' | transloco }}
      </a>
      <a
        routerLink="/attendance/settings/branches"
        class="rounded-md px-3 py-2 text-sm font-medium text-fg-2 hover:bg-hover"
        [class.bg-hover]="active() === 'branches'"
        [class.text-fg]="active() === 'branches'"
      >
        {{ 'attendance.settings.nav.branches' | transloco }}
      </a>
      <a
        routerLink="/attendance/settings/holidays"
        class="rounded-md px-3 py-2 text-sm font-medium text-fg-2 hover:bg-hover"
        [class.bg-hover]="active() === 'holidays'"
        [class.text-fg]="active() === 'holidays'"
      >
        {{ 'attendance.settings.nav.holidays' | transloco }}
      </a>
    </nav>
  `,
})
export class AttendanceSettingsSubnavComponent {
  readonly active = input<'rules' | 'branches' | 'holidays'>('rules');
}
