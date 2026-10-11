import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ThemeService } from '../../core/theme/theme.service';
import { IconComponent } from '../../shared/ui/icon/icon.component';

@Component({
  selector: 'kadr-auth-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe, IconComponent],
  template: `
    <div class="flex min-h-dvh flex-col bg-app">
      <header
        class="flex items-center justify-between gap-3 border-b border-line bg-surface px-4 py-3 md:px-6"
      >
        <div class="flex min-w-0 items-center gap-3">
          <picture>
            <source type="image/webp" srcset="/brand/logo-kadr-128.webp" />
            <img
              src="/brand/logo-kadr-128.png"
              alt=""
              width="36"
              height="36"
              class="size-9 shrink-0 object-contain"
            />
          </picture>
          <div class="min-w-0 text-start">
            <p class="truncate text-sm font-bold text-fg">{{ 'auth.shell.brand' | transloco }}</p>
            <p class="truncate text-xs text-fg-muted">{{ 'auth.shell.brandLine' | transloco }}</p>
          </div>
        </div>
        <button
          type="button"
          class="inline-flex h-11 items-center gap-2 rounded-md px-3 text-sm text-fg-2 hover:bg-hover focus-visible:outline-none"
          [attr.aria-label]="'uiKit.language' | transloco"
          (click)="toggleLanguage()"
        >
          <kadr-icon name="globe" size="sm" />
          {{ theme.language() === 'ar' ? ('uiKit.langEn' | transloco) : ('uiKit.langAr' | transloco) }}
        </button>
      </header>

      <main class="mx-auto flex w-full max-w-5xl flex-1 flex-col justify-center px-4 py-8 md:px-6">
        <div
          class="auth-shell-card overflow-hidden rounded-lg border border-line bg-surface shadow-sm md:grid md:grid-cols-[1.4fr_1fr]"
          data-testid="auth-shell-card"
        >
          <!-- Form column: inline-start (right in RTL) -->
          <div class="flex flex-col gap-6 p-6 sm:p-8" data-testid="auth-form-column">
            <header class="flex flex-col gap-3 text-start">
              <div class="flex items-center gap-3">
                <picture>
                  <source type="image/webp" srcset="/brand/logo-kadr-128.webp" />
                  <img
                    src="/brand/logo-kadr-128.png"
                    alt=""
                    width="40"
                    height="40"
                    class="size-10 object-contain"
                  />
                </picture>
                <div>
                  <p class="text-sm font-bold text-fg">{{ 'auth.shell.brand' | transloco }}</p>
                  <p class="text-xs text-fg-muted">{{ 'auth.shell.brandLine' | transloco }}</p>
                </div>
              </div>
              @if (showSecureBadge()) {
                <p
                  class="inline-flex w-fit items-center gap-2 rounded-md bg-subtle px-2.5 py-1 text-xs text-fg-2"
                >
                  <kadr-icon name="lock" size="sm" />
                  {{ 'auth.shell.secureBadge' | transloco }}
                </p>
              }
              <div class="flex flex-col gap-1">
                <h1 class="text-xl font-bold leading-tight text-fg sm:text-2xl">{{ title() }}</h1>
                @if (subtitle()) {
                  <p class="text-sm text-fg-2">{{ subtitle() }}</p>
                }
              </div>
            </header>

            <ng-content />

            <ng-content select="[authFooter]" />
          </div>

          <!-- Brand panel: inline-end (left in RTL); hidden under md -->
          <aside
            class="hidden flex-col justify-between gap-6 bg-accent p-6 text-on-accent sm:p-8 md:flex"
            data-testid="auth-brand-panel"
            [attr.aria-label]="'auth.panel.aria' | transloco"
          >
            <div class="flex flex-col gap-5">
              <p
                class="inline-flex w-fit items-center gap-2 rounded-md bg-surface/15 px-3 py-1 text-xs font-medium"
              >
                <kadr-icon name="shield" size="sm" />
                {{ 'auth.panel.badge' | transloco }}
              </p>
              <h2 class="text-2xl font-bold leading-tight">
                {{ 'auth.panel.headline' | transloco }}
              </h2>
              <ul class="flex flex-col gap-3">
                <li class="rounded-md border border-surface/20 bg-surface/10 p-3">
                  <div class="flex items-start gap-3">
                    <kadr-icon name="building" size="sm" />
                    <div class="text-start">
                      <p class="text-sm font-medium">{{ 'auth.panel.f1Title' | transloco }}</p>
                      <p class="mt-1 text-xs text-on-accent/90">{{ 'auth.panel.f1Body' | transloco }}</p>
                    </div>
                  </div>
                </li>
                <li class="rounded-md border border-surface/20 bg-surface/10 p-3">
                  <div class="flex items-start gap-3">
                    <kadr-icon name="file" size="sm" />
                    <div class="text-start">
                      <p class="text-sm font-medium">{{ 'auth.panel.f2Title' | transloco }}</p>
                      <p class="mt-1 text-xs text-on-accent/90">{{ 'auth.panel.f2Body' | transloco }}</p>
                    </div>
                  </div>
                </li>
                <li class="rounded-md border border-surface/20 bg-surface/10 p-3">
                  <div class="flex items-start gap-3">
                    <kadr-icon name="shield" size="sm" />
                    <div class="text-start">
                      <p class="text-sm font-medium">{{ 'auth.panel.f3Title' | transloco }}</p>
                      <p class="mt-1 text-xs text-on-accent/90">{{ 'auth.panel.f3Body' | transloco }}</p>
                    </div>
                  </div>
                </li>
              </ul>
            </div>
            <p class="text-xs text-on-accent/80">{{ 'auth.panel.footer' | transloco }}</p>
          </aside>
        </div>

        <footer
          class="mt-6 flex flex-col items-center gap-2 text-center text-xs text-fg-muted md:flex-row md:justify-between md:text-start"
        >
          <p class="hidden md:block">{{ 'auth.shell.badges' | transloco }}</p>
          <nav class="flex flex-wrap items-center justify-center gap-3" [attr.aria-label]="'auth.shell.footerNav' | transloco">
            <span>{{ 'auth.shell.privacy' | transloco }}</span>
            <span aria-hidden="true">·</span>
            <span>{{ 'auth.shell.terms' | transloco }}</span>
          </nav>
          <p>{{ 'auth.shell.copyright' | transloco }}</p>
        </footer>
      </main>
    </div>
  `,
})
export class AuthShellLayout {
  private readonly transloco = inject(TranslocoService);
  protected readonly theme = inject(ThemeService);

  readonly title = input.required<string>();
  readonly subtitle = input<string | undefined>(undefined);
  readonly showSecureBadge = input(true);

  toggleLanguage(): void {
    const next = this.theme.language() === 'ar' ? 'en' : 'ar';
    this.theme.setLanguage(next);
    this.transloco.setActiveLang(next);
  }
}
