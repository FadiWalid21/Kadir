import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { InstallPromptService } from '../../../core/pwa/install-prompt.service';
import { ButtonComponent } from '../button/button.component';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'kadr-ios-install-sheet',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent, IconComponent],
  template: `
    @if (install.showHowTo()) {
      <div
        class="fixed inset-0 z-50 flex items-end justify-center sm:items-center"
        role="presentation"
        [attr.data-testid]="install.isIos() ? 'ios-install-sheet' : 'browser-install-dialog'"
      >
        <button
          type="button"
          class="absolute inset-0 bg-fg/40"
          [attr.aria-label]="'ui.close' | transloco"
          (click)="install.closeHowTo()"
        ></button>
        <div
          class="kadr-drawer-sheet relative z-10 flex w-full max-w-lg flex-col gap-4 rounded-t-lg border border-line bg-surface p-4 shadow-lg sm:rounded-lg"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
        >
          <div class="flex justify-center sm:hidden" aria-hidden="true">
            <span class="h-1 w-10 rounded-full bg-line-strong"></span>
          </div>
          @if (install.isIos()) {
            <h2 [id]="titleId" class="text-start text-lg font-bold text-fg">
              {{ 'pwa.ios.title' | transloco }}
            </h2>
            <ol class="flex flex-col gap-3 text-start text-sm text-fg">
              <li class="flex items-start gap-3">
                <span class="mt-0.5 text-fg-2"><kadr-icon name="share" size="md" /></span>
                <span>{{ 'pwa.ios.step1' | transloco }}</span>
              </li>
              <li class="flex items-start gap-3">
                <span class="mt-0.5 text-fg-2"><kadr-icon name="plus-square" size="md" /></span>
                <span>{{ 'pwa.ios.step2' | transloco }}</span>
              </li>
              <li class="flex items-start gap-3">
                <span class="mt-0.5 text-fg-2"><kadr-icon name="check" size="md" /></span>
                <span>{{ 'pwa.ios.step3' | transloco }}</span>
              </li>
            </ol>
            <p class="text-start text-xs text-fg-muted">{{ 'pwa.ios.safariOnly' | transloco }}</p>
            <kadr-button variant="primary" type="button" class="w-full" (click)="install.closeHowTo()">
              {{ 'pwa.ios.gotIt' | transloco }}
            </kadr-button>
            <button
              type="button"
              class="text-center text-sm text-fg-2 underline-offset-2 hover:underline focus-visible:outline-none"
              data-testid="ios-never"
              (click)="install.dismissBanner()"
            >
              {{ 'pwa.ios.never' | transloco }}
            </button>
          } @else {
            <h2 [id]="titleId" class="text-start text-lg font-bold text-fg">
              {{ 'pwa.browser.title' | transloco }}
            </h2>
            <p class="text-start text-sm text-fg-2">{{ 'pwa.browser.body' | transloco }}</p>
            <ol class="flex list-decimal flex-col gap-2 ps-5 text-start text-sm text-fg">
              <li>{{ 'pwa.browser.step1' | transloco }}</li>
              <li>{{ 'pwa.browser.step2' | transloco }}</li>
            </ol>
            <kadr-button variant="primary" type="button" class="w-full" (click)="install.closeHowTo()">
              {{ 'pwa.ios.gotIt' | transloco }}
            </kadr-button>
          }
        </div>
      </div>
    }
  `,
})
export class IosInstallSheetComponent {
  protected readonly install = inject(InstallPromptService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly titleId = 'install-howto-title';

  constructor() {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && this.install.showHowTo()) {
        this.install.closeHowTo();
      }
    };
    this.document.addEventListener('keydown', onKey);
    this.destroyRef.onDestroy(() => this.document.removeEventListener('keydown', onKey));

    effect(() => {
      this.document.body.style.overflow = this.install.showHowTo() ? 'hidden' : '';
    });
  }
}
