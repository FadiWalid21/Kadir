import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import ar from '../../../assets/i18n/ar.json';
import en from '../../../assets/i18n/en.json';

const landingDir = path.resolve(import.meta.dirname);

function getByPath(root: unknown, dotted: string): unknown {
  return dotted.split('.').reduce<unknown>((acc, part) => {
    if (acc == null || typeof acc !== 'object') return undefined;
    return (acc as Record<string, unknown>)[part];
  }, root);
}

describe('landing translation keys used in feature files', () => {
  it('every landing.* string literal resolves in ar and en', () => {
    const files = readdirSync(landingDir).filter((f) => f.endsWith('.ts') && !f.endsWith('.spec.ts'));
    const keys = new Set<string>();
    for (const file of files) {
      const src = readFileSync(path.join(landingDir, file), 'utf8');
      for (const m of src.matchAll(/['"](landing\.[a-zA-Z0-9_.]+)['"]/g)) {
        keys.add(m[1]!);
      }
    }
    expect(keys.size).toBeGreaterThan(30);
    const missingAr: string[] = [];
    const missingEn: string[] = [];
    for (const key of [...keys].sort()) {
      if (getByPath(ar, key) === undefined) missingAr.push(key);
      if (getByPath(en, key) === undefined) missingEn.push(key);
    }
    expect(missingAr, `missing in ar.json: ${missingAr.join(', ')}`).toEqual([]);
    expect(missingEn, `missing in en.json: ${missingEn.join(', ')}`).toEqual([]);
  });
});
