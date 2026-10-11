import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { InstallService } from '../../../core/pwa/install.service';
import { ButtonComponent } from '../button/button.component';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'kadr-ios-install-sheet',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, ButtonComponent, IconComponent],
  template: `
    @if (install.showIosSheet()) {
      <div class="fixed inset-0 z-50 flex items-end justify-center" role="presentation" data-testid="ios-install-sheet">
        <button
          type="button"
          class="absolute inset-0 bg-fg/40"
          [attr.aria-label]="'ui.close' | transloco"
          (click)="install.closeIosSheet()"
        ></button>
        <div
          class="kadr-drawer-sheet relative z-10 flex w-full max-w-lg flex-col gap-4 rounded-t-lg border border-line bg-surface p-4 shadow-lg"
          role="dialog"
          aria-modal="true"
          [attr.aria-labelledby]="titleId"
        >
          <div class="flex justify-center" aria-hidden="true">
            <span class="h-1 w-10 rounded-full bg-line-strong"></span>
          </div>
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
          <kadr-button variant="primary" type="button" class="w-full" (click)="install.closeIosSheet()">
            {{ 'pwa.ios.gotIt' | transloco }}
          </kadr-button>
          <button
            type="button"
            class="text-center text-sm text-fg-2 underline-offset-2 hover:underline focus-visible:outline-none"
            data-testid="ios-never"
            (click)="install.neverShowIosSheet()"
          >
            {{ 'pwa.ios.never' | transloco }}
          </button>
        </div>
      </div>
    }
  `,
})
export class IosInstallSheetComponent {
  protected readonly install = inject(InstallService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly titleId = 'ios-install-title';

  constructor() {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && this.install.showIosSheet()) {
        this.install.closeIosSheet();
      }
    };
    this.document.addEventListener('keydown', onKey);
    this.destroyRef.onDestroy(() => this.document.removeEventListener('keydown', onKey));

    effect(() => {
      this.document.body.style.overflow = this.install.showIosSheet() ? 'hidden' : '';
    });
  }
}
