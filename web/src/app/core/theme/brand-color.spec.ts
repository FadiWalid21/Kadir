import { describe, expect, it } from 'vitest';
import {
  contrastRatio,
  deriveOnBrand,
  ensureBrandButtonReady,
  hexToHue,
  isHueNearDanger,
  NEAR_BLACK,
  WHITE,
} from './brand-color';

describe('brand-color', () => {
  it('picks white on-brand for burgundy with AA contrast', () => {
    const on = deriveOnBrand('#7A1F3D');
    expect(on).toBe(WHITE);
    expect(contrastRatio('#7A1F3D', on)).toBeGreaterThanOrEqual(4.5);
  });

  it('picks near-black on-brand for a light pastel', () => {
    const on = deriveOnBrand('#F5D0D8');
    expect(on).toBe(NEAR_BLACK);
    expect(contrastRatio('#F5D0D8', on)).toBeGreaterThanOrEqual(4.5);
  });

  it('darkens a too-light brand until button text contrast passes', () => {
    const { brand, onBrand } = ensureBrandButtonReady('#FFF0F5');
    expect(contrastRatio(brand, onBrand)).toBeGreaterThanOrEqual(4.5);
    expect(brand.toUpperCase()).not.toBe('#FFF0F5');
  });

  it('flags hues close to danger red', () => {
    expect(isHueNearDanger('#B42318')).toBe(true);
    expect(isHueNearDanger('#0F766E')).toBe(false);
    expect(hexToHue('#7A1F3D')).toBeGreaterThan(300);
  });
});
