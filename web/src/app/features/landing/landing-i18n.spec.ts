import { describe, expect, it } from 'vitest';
import ar from '../../../assets/i18n/ar.json';
import en from '../../../assets/i18n/en.json';

function flatten(obj: unknown, prefix = ''): string[] {
  if (obj == null || typeof obj !== 'object') {
    return prefix ? [prefix] : [];
  }
  return Object.entries(obj as Record<string, unknown>).flatMap(([k, v]) => {
    const path = prefix ? `${prefix}.${k}` : k;
    if (v != null && typeof v === 'object' && !Array.isArray(v)) {
      return flatten(v, path);
    }
    return [path];
  });
}

function getByPath(root: unknown, path: string): unknown {
  return path.split('.').reduce<unknown>((acc, part) => {
    if (acc == null || typeof acc !== 'object') return undefined;
    return (acc as Record<string, unknown>)[part];
  }, root);
}

describe('landing i18n key parity', () => {
  it('has matching landing.* keys in ar and en', () => {
    const arKeys = flatten((ar as { landing: unknown }).landing, 'landing').sort();
    const enKeys = flatten((en as { landing: unknown }).landing, 'landing').sort();
    expect(arKeys).toEqual(enKeys);
    expect(enKeys.length).toBeGreaterThan(20);
  });

  it('includes all five industry template codes', () => {
    for (const code of ['retail', 'clinic', 'factory', 'construction', 'office']) {
      expect(getByPath(ar, `landing.industries.${code}`)).toEqual(expect.any(String));
      expect(getByPath(en, `landing.industries.${code}`)).toEqual(expect.any(String));
    }
  });

  it('has no coming-soon or country-tied marketing copy in ar/en landing strings', () => {
    const arFlat = flatten((ar as { landing: unknown }).landing, 'landing');
    const enFlat = flatten((en as { landing: unknown }).landing, 'landing');
    const values = [...arFlat, ...enFlat].map((k) => String(getByPath(arFlat.includes(k) ? ar : en, k)));
    // Re-read properly from both trees
    const allValues: string[] = [];
    for (const k of arFlat) {
      allValues.push(String(getByPath(ar, k)));
    }
    for (const k of enFlat) {
      allValues.push(String(getByPath(en, k)));
    }
    const joined = allValues.join('\n');
    expect(joined).not.toMatch(/قريبًا|coming soon|Coming soon/i);
    expect(joined).not.toMatch(/مصر|Egyptian|Egypt/i);
    expect(joined).not.toMatch(/بيانات تجريبية|معاينة القالب|Sample demo|Template preview/i);
    void values;
  });
});
