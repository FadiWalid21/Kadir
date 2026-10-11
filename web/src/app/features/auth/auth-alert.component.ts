import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IconComponent, IconName } from '../../shared/ui/icon/icon.component';

export type AuthAlertTone = 'info' | 'warning' | 'danger';

/**
 * Inline alert at the top of an auth card: icon + bold title + one line + optional links (projected).
 * The text keeps the normal text color; only the soft background and the icon carry the status.
 */
@Component({
  selector: 'kadr-auth-alert',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconComponent],
  template: `
    <div
      class="flex items-start gap-3 rounded-md px-4 py-3 text-start"
      [class]="surface()"
      [attr.role]="tone() === 'info' ? 'status' : 'alert'"
    >
      <span [class]="iconColor()" class="mt-0.5">
        <kadr-icon [name]="icon()" size="md" />
      </span>
      <div class="flex min-w-0 flex-1 flex-col gap-1">
        <p class="text-sm font-bold text-fg">{{ title() }}</p>
        <p class="text-sm text-fg">{{ message() }}</p>
        <div class="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm">
          <ng-content />
        </div>
      </div>
    </div>
  `,
})
export class AuthAlertComponent {
  readonly tone = input.required<AuthAlertTone>();
  readonly icon = input.required<IconName>();
  readonly title = input.required<string>();
  readonly message = input.required<string>();

  protected readonly surface = computed(() => {
    switch (this.tone()) {
      case 'info':
        return 'bg-info-soft';
      case 'warning':
        return 'bg-warning-soft';
      default:
        return 'bg-danger-soft';
    }
  });

  protected readonly iconColor = computed(() => {
    switch (this.tone()) {
      case 'info':
        return 'text-info';
      case 'warning':
        return 'text-warning';
      default:
        return 'text-danger';
    }
  });
}
