import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { contrastRatio } from './brand-color';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  let service: ThemeService;
  let documentRef: Document;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [ThemeService],
    });
    documentRef = TestBed.inject(DOCUMENT);
    service = TestBed.inject(ThemeService);
  });

  afterEach(() => {
    localStorage.clear();
    documentRef.documentElement.removeAttribute('data-theme');
    documentRef.documentElement.removeAttribute('data-density');
    documentRef.documentElement.style.removeProperty('--brand-base');
    documentRef.documentElement.style.removeProperty('--on-brand');
  });

  it('defaults to Arabic RTL and applies html attributes', () => {
    expect(service.language()).toBe('ar');
    expect(service.direction()).toBe('rtl');
    expect(documentRef.documentElement.lang).toBe('ar');
    expect(documentRef.documentElement.dir).toBe('rtl');
    expect(documentRef.documentElement.dataset['density']).toBe('comfortable');
  });

  it('persists theme and density to localStorage', () => {
    service.setTheme('dark');
    service.setDensity('compact');
    expect(localStorage.getItem('kadr.theme')).toBe('dark');
    expect(localStorage.getItem('kadr.density')).toBe('compact');
    expect(documentRef.documentElement.dataset['theme']).toBe('dark');
    expect(documentRef.documentElement.dataset['density']).toBe('compact');
  });

  it('switches language and direction', () => {
    service.setLanguage('en');
    expect(service.direction()).toBe('ltr');
    expect(documentRef.documentElement.lang).toBe('en');
    expect(documentRef.documentElement.dir).toBe('ltr');
    expect(localStorage.getItem('kadr.language')).toBe('en');
  });

  it('survives localStorage write failures', () => {
    const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('quota');
    });
    expect(() => service.setTheme('light')).not.toThrow();
    spy.mockRestore();
  });

  it('setBrand sets CSS vars with AA on-brand contrast', () => {
    service.setBrand('#7A1F3D');
    const brand = documentRef.documentElement.style.getPropertyValue('--brand-base');
    const onBrand = documentRef.documentElement.style.getPropertyValue('--on-brand');
    expect(brand).toBe('#7A1F3D');
    expect(contrastRatio(brand, onBrand)).toBeGreaterThanOrEqual(4.5);
  });

  it('setBrand darkens light colors and flags near-danger hues', () => {
    service.setBrand('#FFCCCC');
    const brand = documentRef.documentElement.style.getPropertyValue('--brand-base');
    const onBrand = documentRef.documentElement.style.getPropertyValue('--on-brand');
    expect(contrastRatio(brand, onBrand)).toBeGreaterThanOrEqual(4.5);
    expect(brand.toUpperCase()).not.toBe('#FFCCCC');

    service.setBrand('#B42318');
    expect(service.brandNearDanger()).toBe(true);
  });
});
