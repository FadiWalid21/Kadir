/** Near-black used for on-brand text when white fails AA contrast. */
export const NEAR_BLACK = '#0B1012';
export const WHITE = '#FFFFFF';
/** Product default brand (matches tokens.css --brand-base). */
export const DEFAULT_BRAND_HEX = '#7A1F3D';
/** Danger red from tokens; used for near-danger hue warning. */
export const DANGER_HEX = '#B42318';
/** Circular hue distance (degrees) treated as "close" to danger. */
export const NEAR_DANGER_HUE_THRESHOLD = 25;

export interface Rgb {
  r: number;
  g: number;
  b: number;
}

export function normalizeHex(hex: string): string {
  const raw = hex.trim().replace(/^#/, '');
  if (!/^[0-9a-fA-F]{6}$/.test(raw)) {
    throw new Error(`Invalid hex color: ${hex}`);
  }
  return `#${raw.toUpperCase()}`;
}

export function hexToRgb(hex: string): Rgb {
  const h = normalizeHex(hex).slice(1);
  return {
    r: parseInt(h.slice(0, 2), 16),
    g: parseInt(h.slice(2, 4), 16),
    b: parseInt(h.slice(4, 6), 16),
  };
}

export function rgbToHex({ r, g, b }: Rgb): string {
  const to = (n: number) =>
    Math.max(0, Math.min(255, Math.round(n)))
      .toString(16)
      .padStart(2, '0')
      .toUpperCase();
  return `#${to(r)}${to(g)}${to(b)}`;
}

/** Relative luminance (sRGB, WCAG 2.x). */
export function relativeLuminance(hex: string): number {
  const { r, g, b } = hexToRgb(hex);
  const channel = (c: number) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
}

export function contrastRatio(hexA: string, hexB: string): number {
  const a = relativeLuminance(hexA);
  const b = relativeLuminance(hexB);
  const lighter = Math.max(a, b);
  const darker = Math.min(a, b);
  return (lighter + 0.05) / (darker + 0.05);
}

/** Choose white or near-black for text on `bg`, preferring contrast ≥ 4.5. */
export function deriveOnBrand(bgHex: string): string {
  const whiteRatio = contrastRatio(bgHex, WHITE);
  const blackRatio = contrastRatio(bgHex, NEAR_BLACK);
  if (whiteRatio >= 4.5 || blackRatio >= 4.5) {
    return whiteRatio >= blackRatio ? WHITE : NEAR_BLACK;
  }
  return whiteRatio >= blackRatio ? WHITE : NEAR_BLACK;
}

/** Mix color toward black by `amount` (0–1). */
export function darkenHex(hex: string, amount: number): string {
  const { r, g, b } = hexToRgb(hex);
  const t = Math.max(0, Math.min(1, amount));
  return rgbToHex({ r: r * (1 - t), g: g * (1 - t), b: b * (1 - t) });
}

/**
 * Ensure brand works as a solid primary-button background.
 * Darkens until white text reaches contrast ≥ 4.5 (typical accent button),
 * then sets --on-brand to white or near-black for the best AA pair.
 */
export function ensureBrandButtonReady(hex: string): { brand: string; onBrand: string } {
  let brand = normalizeHex(hex);
  let guard = 0;
  while (contrastRatio(brand, WHITE) < 4.5 && guard < 48) {
    brand = darkenHex(brand, 0.08);
    guard += 1;
  }
  const onBrand = deriveOnBrand(brand);
  return { brand, onBrand };
}

/** Hue in degrees [0, 360) from sRGB hex. */
export function hexToHue(hex: string): number {
  const { r, g, b } = hexToRgb(hex);
  const rn = r / 255;
  const gn = g / 255;
  const bn = b / 255;
  const max = Math.max(rn, gn, bn);
  const min = Math.min(rn, gn, bn);
  const delta = max - min;
  if (delta === 0) {
    return 0;
  }
  let sector: number;
  if (max === rn) {
    sector = ((gn - bn) / delta) % 6;
  } else if (max === gn) {
    sector = (bn - rn) / delta + 2;
  } else {
    sector = (rn - gn) / delta + 4;
  }
  const hue = sector * 60;
  return hue < 0 ? hue + 360 : hue;
}

export function circularHueDistance(a: number, b: number): number {
  const d = Math.abs(a - b) % 360;
  return Math.min(d, 360 - d);
}

export function isHueNearDanger(hex: string, threshold = NEAR_DANGER_HUE_THRESHOLD): boolean {
  return circularHueDistance(hexToHue(hex), hexToHue(DANGER_HEX)) <= threshold;
}
