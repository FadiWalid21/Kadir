import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { IconComponent, IconName } from '../../shared/ui/icon/icon.component';
import { ToastService } from '../../shared/ui/toast/toast.service';

@Component({
  selector: 'kadr-bottom-nav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, RouterLink, RouterLinkActive, IconComponent],
  template: `
    <nav
      class="kadr-safe-bottom fixed inset-x-0 bottom-0 z-40 flex border-t border-line bg-surface md:hidden"
      [attr.aria-label]="'shell.bottomNav' | transloco"
    >
      <a
        routerLink="/"
        routerLinkActive="text-accent"
        [routerLinkActiveOptions]="{ exact: true }"
        class="flex min-h-14 flex-1 flex-col items-center justify-center gap-1 py-2 text-xs text-fg-2 focus-visible:outline-none"
      >
        <kadr-icon name="home" size="sm" />
        {{ 'shell.nav.home' | transloco }}
      </a>
      <a
        routerLink="/attendance/me"
        routerLinkActive="text-accent"
        class="flex min-h-14 flex-1 flex-col items-center justify-center gap-1 py-2 text-xs text-fg-2 focus-visible:outline-none"
      >
        <kadr-icon name="clock" size="sm" />
        {{ 'shell.nav.attendance' | transloco }}
      </a>
      @for (item of disabledItems; track item.key) {
        <button
          type="button"
          class="flex min-h-14 flex-1 flex-col items-center justify-center gap-1 py-2 text-xs text-fg-muted opacity-60 focus-visible:outline-none"
          (click)="onDisabled()"
        >
          <kadr-icon [name]="item.icon" size="sm" />
          {{ item.key | transloco }}
        </button>
      }
    </nav>
  `,
})
export class BottomNavComponent {
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  protected readonly disabledItems: { key: string; icon: IconName }[] = [
    { key: 'shell.nav.requests', icon: 'inbox' },
    { key: 'shell.nav.profile', icon: 'user' },
  ];

  onDisabled(): void {
    this.toast.show(this.transloco.translate('shell.notAvailableYet'), 'info');
  }
}
