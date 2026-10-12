import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { InstallPromptService } from '../../../core/pwa/install-prompt.service';
import { ButtonComponent } from '../button/button.component';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'kadr-install-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent, IconComponent],
  template: `
    <div
      class="kadr-install-banner-collapse"
      [class.is-open]="install.showBanner()"
      [class.is-closed]="!install.showBanner()"
    >
      <div class="overflow-hidden">
        @if (install.showBanner()) {
          <div
            class="kadr-safe-top border-b border-line bg-accent-soft"
            role="region"
            data-testid="install-banner"
            [attr.aria-label]="'pwa.install.bannerRegion' | transloco"
          >
            <div
              class="mx-auto flex h-install-banner max-w-screen-xl items-center gap-3 px-4 text-start text-sm text-accent-soft-fg"
            >
              <p class="min-w-0 flex-1 truncate font-medium">
                {{ 'pwa.install.bannerTitle' | transloco }}
              </p>
              <kadr-button
                variant="primary"
                size="sm"
                type="button"
                data-testid="install-banner-cta"
                (click)="onCta()"
              >
                {{
                  install.canInstall()
                    ? ('pwa.install.cta' | transloco)
                    : ('pwa.install.howTo' | transloco)
                }}
              </kadr-button>
              <button
                type="button"
                class="inline-flex size-11 shrink-0 items-center justify-center rounded-md text-accent-soft-fg hover:bg-hover focus-visible:outline-none"
                [attr.aria-label]="'pwa.install.dismiss' | transloco"
                data-testid="install-banner-dismiss"
                (click)="install.dismissBanner()"
              >
                <kadr-icon name="x" size="sm" />
              </button>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class InstallBannerComponent {
  protected readonly install = inject(InstallPromptService);

  async onCta(): Promise<void> {
    if (this.install.canInstall()) {
      await this.install.promptInstall();
    } else {
      this.install.openHowToInstall();
    }
  }
}
