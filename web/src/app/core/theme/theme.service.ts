import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  DEFAULT_BRAND_HEX,
  ensureBrandButtonReady,
  isHueNearDanger,
  normalizeHex,
} from './brand-color';
import type { AppLanguage, DensityMode, TextDirection, ThemeMode } from './theme.types';

const STORAGE_THEME = 'kadr.theme';
const STORAGE_DENSITY = 'kadr.density';
const STORAGE_LANG = 'kadr.language';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly mediaQuery =
    typeof matchMedia === 'function' ? matchMedia('(prefers-color-scheme: dark)') : null;
  private mediaListener?: (event: MediaQueryListEvent) => void;

  readonly theme = signal<ThemeMode>('system');
  readonly density = signal<DensityMode>('comfortable');
  readonly language = signal<AppLanguage>('ar');
  readonly brandHex = signal(DEFAULT_BRAND_HEX);
  readonly brandNearDanger = signal(false);

  readonly direction = computed<TextDirection>(() =>
    this.language() === 'ar' ? 'rtl' : 'ltr',
  );

  readonly resolvedTheme = computed<'light' | 'dark'>(() => {
    const mode = this.theme();
    if (mode === 'system') {
      return this.mediaQuery?.matches ? 'dark' : 'light';
    }
    return mode;
  });

  constructor() {
    this.restoreFromStorage();
    this.bindSystemThemeListener();
    this.applyAll();
  }

  setTheme(mode: ThemeMode): void {
    this.theme.set(mode);
    this.persist(STORAGE_THEME, mode);
    this.applyThemeAttributes();
  }

  setDensity(density: DensityMode): void {
    this.density.set(density);
    this.persist(STORAGE_DENSITY, density);
    this.applyThemeAttributes();
  }

  setLanguage(language: AppLanguage): void {
    this.language.set(language);
    this.persist(STORAGE_LANG, language);
    this.applyThemeAttributes();
  }

  /**
   * Runtime tenant/product brand override (ui-kit / Feature 1).
   * Darkens colors that are too light for buttons; sets --on-brand for AA contrast.
   */
  setBrand(hex: string): void {
    const normalized = normalizeHex(hex);
    const { brand, onBrand } = ensureBrandButtonReady(normalized);
    this.brandHex.set(brand);
    this.brandNearDanger.set(isHueNearDanger(brand));
    const root = this.document.documentElement;
    root.style.setProperty('--brand-base', brand);
    root.style.setProperty('--on-brand', onBrand);
  }

  private applyAll(): void {
    this.applyThemeAttributes();
    this.setBrand(this.brandHex());
  }

  private applyThemeAttributes(): void {
    const root = this.document.documentElement;
    root.dataset['theme'] = this.resolvedTheme();
    root.dataset['density'] = this.density();
    root.lang = this.language();
    root.dir = this.direction();
    this.syncThemeColorMeta();
  }

  /** Keep the browser chrome in sync with the surface token (never hard-coded hex in components). */
  private syncThemeColorMeta(): void {
    const meta = this.document.querySelector('meta[name="theme-color"]');
    if (!meta) {
      return;
    }
    const surface = getComputedStyle(this.document.documentElement)
      .getPropertyValue('--bg-surface')
      .trim();
    if (surface) {
      meta.setAttribute('content', surface);
    }
  }

  private bindSystemThemeListener(): void {
    if (!this.mediaQuery) {
      return;
    }
    this.mediaListener = () => {
      if (this.theme() === 'system') {
        this.applyThemeAttributes();
      }
    };
    this.mediaQuery.addEventListener('change', this.mediaListener);
  }

  private restoreFromStorage(): void {
    const theme = this.read(STORAGE_THEME);
    if (theme === 'light' || theme === 'dark' || theme === 'system') {
      this.theme.set(theme);
    }
    const density = this.read(STORAGE_DENSITY);
    if (density === 'comfortable' || density === 'compact') {
      this.density.set(density);
    }
    const language = this.read(STORAGE_LANG);
    if (language === 'ar' || language === 'en') {
      this.language.set(language);
    }
  }

  private persist(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      /* private mode / quota */
    }
  }

  private read(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }
}
