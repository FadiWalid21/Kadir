import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { startWith } from 'rxjs';
import { PublicPlansApi } from '../../core/api/public-plans.api';
import { PlanCatalogItemDto } from '../../core/api/subscription.models';
import { ThemeService } from '../../core/theme/theme.service';
import { environment } from '../../../environments/environment';
import { AccordionComponent, AccordionItem } from '../../shared/ui/accordion/accordion.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { KadrRevealDirective } from './kadr-reveal.directive';
import { LandingBentoComponent } from './landing-bento.component';
import { LandingHowComponent } from './landing-how.component';
import { LandingIndustriesComponent } from './landing-industries.component';
import { LandingProductMockComponent } from './landing-product-mock.component';
import { LandingWordmarkComponent } from './landing-wordmark.component';

type SectionId = 'how' | 'features' | 'industries' | 'pricing' | 'faq';

@Component({
  selector: 'kadr-landing-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslocoPipe,
    ButtonComponent,
    IconComponent,
    AccordionComponent,
    SkeletonComponent,
    KadrRevealDirective,
    LandingProductMockComponent,
    LandingWordmarkComponent,
    LandingHowComponent,
    LandingBentoComponent,
    LandingIndustriesComponent,
  ],
  template: `
    <a
      href="#landing-main"
      class="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-50 focus:rounded-md focus:bg-surface focus:px-3 focus:py-2 focus:text-fg"
    >
      {{ 'landing.skip' | transloco }}
    </a>

    <header
      class="sticky top-0 z-40 border-b bg-surface transition-[border-color,box-shadow,height] duration-[var(--dur)] ease-[var(--ease)]"
      [class.border-transparent]="!condensed()"
      [class.border-line]="condensed()"
      [class.shadow-sm]="condensed()"
    >
      <div class="mx-auto flex h-16 max-w-6xl items-center gap-3 px-4 md:px-6" [class.h-14]="condensed()">
        <a routerLink="/" class="shrink-0 focus-visible:outline-none" [attr.aria-label]="'app.name' | transloco">
          <kadr-landing-wordmark size="md" />
        </a>
        <nav
          class="ms-auto hidden items-center gap-5 lg:flex"
          [attr.aria-label]="'landing.nav.features' | transloco"
        >
          @for (link of navLinks; track link.id) {
            <a
              class="text-sm focus-visible:outline-none"
              [class.text-accent]="activeSection() === link.id"
              [class.font-medium]="activeSection() === link.id"
              [class.text-fg-2]="activeSection() !== link.id"
              [class.hover:text-fg]="activeSection() !== link.id"
              [href]="'#' + link.id"
            >
              {{ link.key | transloco }}
            </a>
          }
        </nav>
        <div class="ms-auto flex items-center gap-1 lg:ms-3">
          <button
            type="button"
            class="inline-flex size-11 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
            [attr.aria-label]="'uiKit.language' | transloco"
            (click)="toggleLanguage()"
          >
            <kadr-icon name="globe" size="sm" />
          </button>
          <button
            type="button"
            class="inline-flex size-11 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none"
            [attr.aria-label]="'uiKit.theme' | transloco"
            (click)="toggleTheme()"
          >
            <kadr-icon [name]="theme.resolvedTheme() === 'dark' ? 'sun' : 'moon'" size="sm" />
          </button>
          <a
            routerLink="/login"
            class="hidden rounded-md px-3 py-2 text-sm text-fg-2 hover:bg-hover focus-visible:outline-none sm:inline-flex"
          >
            {{ 'landing.nav.login' | transloco }}
          </a>
          <a routerLink="/register" class="inline-flex">
            <kadr-button variant="primary" type="button" size="sm">
              {{ 'landing.cta.primary' | transloco }}
            </kadr-button>
          </a>
          <button
            type="button"
            class="inline-flex size-11 items-center justify-center rounded-md text-fg-2 hover:bg-hover focus-visible:outline-none lg:hidden"
            [attr.aria-label]="(menuOpen() ? 'landing.nav.closeMenu' : 'landing.nav.menu') | transloco"
            [attr.aria-expanded]="menuOpen()"
            (click)="menuOpen.set(!menuOpen())"
          >
            <kadr-icon [name]="menuOpen() ? 'x' : 'more-horizontal'" size="sm" />
          </button>
        </div>
      </div>
      @if (menuOpen()) {
        <div class="border-t border-line bg-surface px-4 py-3 lg:hidden">
          <nav class="flex flex-col gap-2 text-start" [attr.aria-label]="'landing.nav.features' | transloco">
            @for (link of navLinks; track link.id) {
              <a
                class="rounded-md px-2 py-2 text-sm text-fg hover:bg-hover focus-visible:outline-none"
                [href]="'#' + link.id"
                (click)="menuOpen.set(false)"
              >
                {{ link.key | transloco }}
              </a>
            }
            <a routerLink="/login" class="rounded-md px-2 py-2 text-sm text-fg-2" (click)="menuOpen.set(false)">
              {{ 'landing.nav.login' | transloco }}
            </a>
          </nav>
        </div>
      }
    </header>

    <main id="landing-main" class="bg-app text-fg">
      <section class="mx-auto grid max-w-6xl gap-10 px-4 py-12 md:grid-cols-2 md:items-center md:px-6 md:py-16">
        <div class="text-start" kadrReveal>
          <h1 class="font-bold text-fg leading-tight" style="font-size: clamp(1.75rem, 4vw + 1rem, 3rem)">
            {{ 'landing.hero.title' | transloco }}
          </h1>
          <p class="mt-4 max-w-xl text-base text-fg-2">{{ 'landing.hero.subtitle' | transloco }}</p>
          <div class="mt-6 flex flex-wrap items-center gap-3">
            <a routerLink="/register">
              <kadr-button variant="primary" type="button" size="lg">
                {{ 'landing.cta.primary' | transloco }}
              </kadr-button>
            </a>
            <p class="text-xs text-fg-muted">{{ 'landing.trust.trial' | transloco }}</p>
          </div>
          <p class="mt-2 text-xs text-fg-muted">{{ 'landing.trust.setup' | transloco }}</p>
        </div>
        <div kadrReveal [kadrRevealDelay]="80">
          <kadr-landing-product-mock />
        </div>
      </section>

      <section id="how" class="border-y border-line bg-surface py-14">
        <div class="mx-auto max-w-6xl px-4 md:px-6">
          <div class="text-start" kadrReveal>
            <h2 class="text-2xl font-bold text-fg">{{ 'landing.how.title' | transloco }}</h2>
            <p class="mt-2 max-w-2xl text-sm text-fg-2">{{ 'landing.how.subtitle' | transloco }}</p>
          </div>
          <div class="mt-8" kadrReveal [kadrRevealDelay]="40">
            <kadr-landing-how
              [active]="howActive()"
              [progress]="howProgress()"
              (stepSelect)="onHowSelect($event)"
            />
          </div>
        </div>
      </section>

      <section id="features" class="py-14">
        <div class="mx-auto max-w-6xl px-4 md:px-6">
          <div class="text-start" kadrReveal>
            <h2 class="text-2xl font-bold text-fg">{{ 'landing.capabilities.title' | transloco }}</h2>
            <p class="mt-2 max-w-2xl text-sm text-fg-2">{{ 'landing.capabilities.subtitle' | transloco }}</p>
          </div>
          <div class="mt-8" kadrReveal [kadrRevealDelay]="40">
            <kadr-landing-bento />
          </div>
        </div>
      </section>

      <section id="industries" class="border-y border-line bg-surface py-14">
        <div class="mx-auto max-w-6xl px-4 md:px-6">
          <div class="text-start" kadrReveal>
            <h2 class="text-2xl font-bold text-fg">{{ 'landing.industries.title' | transloco }}</h2>
            <p class="mt-2 text-sm text-fg-2">{{ 'landing.industries.subtitle' | transloco }}</p>
          </div>
          <div class="mt-6" kadrReveal [kadrRevealDelay]="40">
            <kadr-landing-industries />
          </div>
        </div>
      </section>

      <section id="pricing" class="py-14">
        <div class="mx-auto max-w-6xl px-4 md:px-6">
          <div class="text-start" kadrReveal>
            <h2 class="text-2xl font-bold text-fg">{{ 'landing.pricing.title' | transloco }}</h2>
            <p class="mt-2 text-sm text-fg-2">{{ 'landing.pricing.subtitle' | transloco }}</p>
            <p class="mt-2 text-xs text-fg-muted">{{ 'landing.pricing.indicative' | transloco }}</p>
          </div>
          <div class="mt-4 inline-flex rounded-md border border-line bg-subtle p-1" role="group" kadrReveal>
            <button
              type="button"
              class="rounded-sm px-3 py-1.5 text-sm focus-visible:outline-none"
              [class.bg-surface]="pricingView() === 'flat'"
              [class.text-fg]="pricingView() === 'flat'"
              [class.text-fg-2]="pricingView() !== 'flat'"
              (click)="pricingView.set('flat')"
            >
              {{ 'landing.pricing.flat' | transloco }}
            </button>
            <button
              type="button"
              class="rounded-sm px-3 py-1.5 text-sm focus-visible:outline-none"
              [class.bg-surface]="pricingView() === 'perEmployee'"
              [class.text-fg]="pricingView() === 'perEmployee'"
              [class.text-fg-2]="pricingView() !== 'perEmployee'"
              (click)="pricingView.set('perEmployee')"
            >
              {{ 'landing.pricing.perEmployee' | transloco }}
            </button>
          </div>

          @if (plansState() === 'loading') {
            <div class="mt-6 grid gap-3 md:grid-cols-3">
              <kadr-skeleton height="10rem" />
              <kadr-skeleton height="10rem" />
              <kadr-skeleton height="10rem" />
            </div>
          } @else if (plansState() === 'error') {
            <div class="mt-6 text-start">
              <p class="text-sm text-fg-2">{{ 'landing.pricing.loadError' | transloco }}</p>
              <kadr-button class="mt-3" variant="secondary" type="button" (click)="loadPlans()">
                {{ 'landing.pricing.retry' | transloco }}
              </kadr-button>
            </div>
          } @else {
            <ul class="mt-6 grid gap-3 md:grid-cols-3">
              @for (plan of visiblePlans(); track plan.code) {
                <li class="rounded-lg border border-line bg-surface p-5 text-start shadow-sm" kadrReveal>
                  <h3 class="text-base font-bold text-fg">{{ planName(plan) }}</h3>
                  <p class="mt-3 text-2xl font-bold tabular-nums text-fg">
                    <span class="text-sm font-medium text-fg-2">{{ 'landing.pricing.from' | transloco }}</span>
                    {{ formatPrice(plan) }}
                    <span class="ms-1 text-base font-medium text-fg-2">{{ currencySymbol(plan.currency) }}</span>
                  </p>
                  @if (plan.employeeCap !== null) {
                    <p class="mt-2 text-sm text-fg-2">
                      {{ 'landing.pricing.cap' | transloco: { cap: formatNumber(plan.employeeCap) } }}
                    </p>
                  }
                  @if (plan.minBilledEmployees !== null) {
                    <p class="text-sm text-fg-2">
                      {{ 'landing.pricing.minBilled' | transloco: { n: formatNumber(plan.minBilledEmployees) } }}
                    </p>
                  }
                  <a routerLink="/register" class="mt-4 inline-flex">
                    <kadr-button variant="primary" type="button" size="sm">
                      {{ 'landing.cta.primary' | transloco }}
                    </kadr-button>
                  </a>
                </li>
              }
            </ul>
          }
        </div>
      </section>

      <section id="faq" class="border-y border-line bg-surface py-14">
        <div class="mx-auto max-w-3xl px-4 md:px-6" kadrReveal>
          <h2 class="text-start text-2xl font-bold text-fg">{{ 'landing.faq.title' | transloco }}</h2>
          <div class="mt-6">
            <kadr-accordion [items]="faqItems()" [ariaLabel]="'landing.faq.aria' | transloco" />
          </div>
        </div>
      </section>

      <section class="relative overflow-hidden bg-inverse py-16 text-on-inverse">
        <svg
          class="pointer-events-none absolute -start-10 top-0 size-48 text-on-inverse/10"
          viewBox="0 0 120 120"
          aria-hidden="true"
        >
          <circle cx="60" cy="60" r="50" fill="none" stroke="currentColor" stroke-width="2" />
          <circle cx="60" cy="60" r="34" fill="none" stroke="currentColor" stroke-width="2" />
        </svg>
        <div class="relative mx-auto max-w-3xl px-4 text-start md:px-6" kadrReveal>
          <kadr-landing-wordmark size="lg" tone="inverse" />
          <h2 class="mt-4 text-2xl font-bold">{{ 'landing.cta.finalTitle' | transloco }}</h2>
          <p class="mt-3 text-sm opacity-90">{{ 'landing.cta.finalBody' | transloco }}</p>
          <a routerLink="/register" class="mt-6 inline-flex">
            <kadr-button variant="secondary" type="button" size="lg">
              {{ 'landing.cta.primary' | transloco }}
            </kadr-button>
          </a>
        </div>
      </section>
    </main>

    <footer class="border-t border-line bg-app py-10">
      <div class="mx-auto flex max-w-6xl flex-col gap-4 px-4 text-start md:flex-row md:items-start md:justify-between md:px-6">
        <div>
          <kadr-landing-wordmark size="md" />
          <p class="mt-2 text-sm text-fg-2">{{ 'landing.footer.tagline' | transloco }}</p>
          @if (contactEmail() || contactWhatsapp()) {
            <div class="mt-3 flex flex-wrap gap-3 text-sm">
              @if (contactEmail(); as email) {
                <a class="text-accent hover:underline focus-visible:outline-none" [href]="'mailto:' + email">
                  <bdi>{{ email }}</bdi>
                </a>
              }
              @if (contactWhatsapp(); as wa) {
                <a
                  class="text-accent hover:underline focus-visible:outline-none"
                  [href]="'https://wa.me/' + wa"
                  rel="noopener noreferrer"
                  target="_blank"
                >
                  <bdi>{{ wa }}</bdi>
                </a>
              }
            </div>
          }
        </div>
        <p class="text-xs text-fg-muted">{{ 'landing.footer.rights' | transloco }}</p>
      </div>
    </footer>
  `,
})
export class LandingPage implements OnInit {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly transloco = inject(TranslocoService);
  private readonly plansApi = inject(PublicPlansApi);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly theme = inject(ThemeService);

  private readonly lang = toSignal(
    this.transloco.langChanges$.pipe(startWith(this.transloco.getActiveLang())),
    { initialValue: this.transloco.getActiveLang() },
  );

  protected readonly condensed = signal(false);
  protected readonly menuOpen = signal(false);
  protected readonly pricingView = signal<'flat' | 'perEmployee'>('flat');
  protected readonly plansState = signal<'loading' | 'ready' | 'error'>('loading');
  protected readonly plans = signal<PlanCatalogItemDto[]>([]);
  protected readonly howProgress = signal(0.15);
  protected readonly howActive = signal(0);
  protected readonly howPinned = signal<number | null>(null);
  protected readonly activeSection = signal<SectionId | null>(null);

  protected readonly navLinks: { id: SectionId; key: string }[] = [
    { id: 'how', key: 'landing.nav.how' },
    { id: 'features', key: 'landing.nav.features' },
    { id: 'industries', key: 'landing.nav.industries' },
    { id: 'pricing', key: 'landing.nav.pricing' },
    { id: 'faq', key: 'landing.nav.faq' },
  ];

  protected readonly faqItems = computed((): AccordionItem[] => {
    this.lang();
    return [1, 2, 3, 4].map((n) => ({
      id: String(n),
      title: this.transloco.translate(`landing.faq.q${n}`),
      body: this.transloco.translate(`landing.faq.a${n}`),
    }));
  });

  protected readonly visiblePlans = computed(() => {
    const model = this.pricingView() === 'flat' ? 'flatTier' : 'perEmployee';
    const all = this.plans();
    const filtered = all.filter((p) => p.pricingModel === model);
    return filtered.length > 0 ? filtered : all;
  });

  protected readonly contactEmail = computed(() => environment.landingContactEmail?.trim() || '');
  protected readonly contactWhatsapp = computed(() => environment.landingContactWhatsapp?.trim() || '');

  ngOnInit(): void {
    this.applyMeta();
    this.transloco.langChanges$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.applyMeta());
    void this.loadPlans();
    this.bindScroll();
    this.injectJsonLd();
  }

  protected onHowSelect(index: number): void {
    this.howPinned.set(index);
    this.howActive.set(index);
    this.howProgress.set(index === 0 ? 0.33 : index === 1 ? 0.66 : 1);
  }

  protected toggleLanguage(): void {
    this.theme.setLanguage(this.theme.language() === 'ar' ? 'en' : 'ar');
    this.transloco.setActiveLang(this.theme.language());
  }

  protected toggleTheme(): void {
    const next = this.theme.resolvedTheme() === 'dark' ? 'light' : 'dark';
    this.theme.setTheme(next);
  }

  protected planName(plan: PlanCatalogItemDto): string {
    return this.lang() === 'en' ? plan.nameEn || plan.nameAr : plan.nameAr;
  }

  protected formatNumber(value: number): string {
    return new Intl.NumberFormat(this.lang() === 'ar' ? 'ar' : 'en', {
      numberingSystem: 'latn',
      maximumFractionDigits: 0,
    }).format(value);
  }

  protected formatPrice(plan: PlanCatalogItemDto): string {
    if (plan.unitPrice == null) return '—';
    return this.formatNumber(plan.unitPrice);
  }

  protected currencySymbol(code: string): string {
    const c = (code || '').toUpperCase();
    if (c === 'EGP') return this.lang() === 'ar' ? 'ج.م' : 'EGP';
    try {
      const parts = new Intl.NumberFormat(this.lang() === 'ar' ? 'ar' : 'en', {
        style: 'currency',
        currency: c,
        currencyDisplay: 'narrowSymbol',
        numberingSystem: 'latn',
      }).formatToParts(0);
      return parts.find((p) => p.type === 'currency')?.value ?? c;
    } catch {
      return c;
    }
  }

  protected async loadPlans(): Promise<void> {
    this.plansState.set('loading');
    try {
      this.plans.set(await this.plansApi.list());
      this.plansState.set('ready');
    } catch {
      this.plansState.set('error');
    }
  }

  private applyMeta(): void {
    const pageTitle = this.transloco.translate('landing.meta.title') || 'Kadr';
    const description = this.transloco.translate('landing.meta.description') || '';
    this.title.setTitle(pageTitle);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ property: 'og:title', content: pageTitle });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ name: 'twitter:card', content: 'summary' });
    const base = environment.landingCanonicalBaseUrl?.trim();
    if (base) {
      this.meta.updateTag({ property: 'og:url', content: `${base}/` });
      let link = this.document.querySelector<HTMLLinkElement>('link[rel="canonical"]');
      if (!link) {
        link = this.document.createElement('link');
        link.rel = 'canonical';
        this.document.head.appendChild(link);
      }
      link.href = `${base}/`;
    }
  }

  private injectJsonLd(): void {
    const id = 'kadr-landing-jsonld';
    this.document.getElementById(id)?.remove();
    const script = this.document.createElement('script');
    script.type = 'application/ld+json';
    script.id = id;
    const base = environment.landingCanonicalBaseUrl?.trim() || undefined;
    script.text = JSON.stringify({
      '@context': 'https://schema.org',
      '@type': 'SoftwareApplication',
      name: 'Kadr',
      applicationCategory: 'BusinessApplication',
      operatingSystem: 'Web',
      description: this.transloco.translate('landing.meta.description'),
      ...(base ? { url: `${base}/` } : {}),
    });
    this.document.head.appendChild(script);
  }

  private bindScroll(): void {
    const onScroll = () => {
      const y = this.document.defaultView?.scrollY ?? 0;
      this.condensed.set(y > 24);

      let current: SectionId | null = null;
      for (const link of this.navLinks) {
        const el = this.document.getElementById(link.id);
        if (!el) continue;
        const top = el.getBoundingClientRect().top;
        if (top <= 120) current = link.id;
      }
      this.activeSection.set(current);

      if (this.howPinned() === null) {
        const how = this.document.getElementById('how');
        if (how) {
          const rect = how.getBoundingClientRect();
          const view = this.document.defaultView?.innerHeight ?? 800;
          const progress = Math.min(1, Math.max(0.15, 1 - rect.top / view));
          this.howProgress.set(progress);
          this.howActive.set(progress < 0.4 ? 0 : progress < 0.7 ? 1 : 2);
        }
      }
    };
    this.document.defaultView?.addEventListener('scroll', onScroll, { passive: true });
    this.destroyRef.onDestroy(() =>
      this.document.defaultView?.removeEventListener('scroll', onScroll),
    );
    onScroll();
  }
}
