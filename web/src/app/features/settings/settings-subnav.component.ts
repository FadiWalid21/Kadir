import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'kadr-settings-subnav',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, TranslocoPipe],
  template: `
    <nav class="flex flex-wrap gap-1 border-b border-line pb-px" [attr.aria-label]="'settings.navAria' | transloco">
      <a
        routerLink="/settings/company"
        routerLinkActive="border-accent text-fg"
        class="border-b-2 border-transparent px-3 py-2 text-sm font-medium text-fg-2 hover:text-fg"
        [class.!border-accent]="active() === 'company'"
        [class.!text-fg]="active() === 'company'"
      >
        {{ 'settings.nav.company' | transloco }}
      </a>
      @if (showTemplates()) {
        <a
          routerLink="/settings/templates"
          routerLinkActive="border-accent text-fg"
          class="border-b-2 border-transparent px-3 py-2 text-sm font-medium text-fg-2 hover:text-fg"
          [class.!border-accent]="active() === 'templates'"
          [class.!text-fg]="active() === 'templates'"
        >
          {{ 'settings.nav.templates' | transloco }}
        </a>
      }
    </nav>
  `,
})
export class SettingsSubnavComponent {
  readonly active = input<'company' | 'templates'>('company');
  readonly showTemplates = input(true);
}
