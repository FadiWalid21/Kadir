import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { InstallPromptService } from './core/pwa/install-prompt.service';
import { InstallBannerComponent } from './shared/ui/pwa/install-banner.component';
import { IosInstallSheetComponent } from './shared/ui/pwa/ios-install-sheet.component';

@Component({
  imports: [RouterOutlet, InstallBannerComponent, IosInstallSheetComponent],
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex min-h-dvh flex-col">
      <kadr-install-banner />
      <div class="min-h-0 min-w-0 flex-1">
        <router-outlet />
      </div>
      <kadr-ios-install-sheet />
    </div>
  `,
})
export class App {
  constructor() {
    // Capture beforeinstallprompt at bootstrap (fires once, often before auth shell).
    inject(InstallPromptService);
  }
}
