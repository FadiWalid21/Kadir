import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { InstallService } from '../../../core/pwa/install.service';
import { ButtonComponent } from '../button/button.component';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'kadr-install-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent, IconComponent],
  template: `
    @if (install.showHomeCard()) {
      <section
        class="mb-6 flex flex-col gap-4 rounded-lg border border-line bg-surface p-4 shadow-sm md:flex-row md:items-center md:justify-between md:p-5"
        data-testid="install-card"
        [attr.aria-label]="'pwa.install.cardTitle' | transloco"
      >
        <div class="flex min-w-0 items-start gap-3 text-start">
          <img
            src="/brand/icon-192.png"
            alt=""
            width="48"
            height="48"
            class="size-12 shrink-0 rounded-md"
          />
          <div class="min-w-0 flex-1">
            <h2 class="text-base font-bold text-fg">{{ 'pwa.install.cardTitle' | transloco }}</h2>
            <p class="mt-1 text-sm text-fg-2">{{ 'pwa.install.cardBody' | transloco }}</p>
          </div>
          <button
            type="button"
            class="inline-flex size-11 shrink-0 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none md:hidden"
            [attr.aria-label]="'pwa.install.dismiss' | transloco"
            data-testid="install-dismiss"
            (click)="install.dismissHomeCard()"
          >
            <kadr-icon name="x" size="sm" />
          </button>
        </div>
        <div class="flex flex-wrap items-center gap-2 md:shrink-0">
          <kadr-button variant="primary" type="button" (click)="onInstall()">
            <kadr-icon name="download" size="sm" class="hidden md:inline" />
            {{ 'pwa.install.cta' | transloco }}
          </kadr-button>
          <kadr-button variant="ghost" type="button" (click)="install.dismissHomeCard()">
            {{ 'pwa.install.notNow' | transloco }}
          </kadr-button>
        </div>
      </section>
    }
  `,
})
export class InstallCardComponent {
  protected readonly install = inject(InstallService);

  async onInstall(): Promise<void> {
    await this.install.install();
  }
}
