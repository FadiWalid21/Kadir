import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

describe('ngsw-config.json', () => {
  it('never caches API data: no dataGroups and no /api patterns', () => {
    const raw = readFileSync(join(process.cwd(), 'ngsw-config.json'), 'utf8');
    const config = JSON.parse(raw) as {
      dataGroups?: unknown[];
      assetGroups?: { resources?: { files?: string[]; urls?: string[] } }[];
      navigationUrls?: string[];
    };

    expect(config.dataGroups ?? []).toEqual([]);

    const patterns: string[] = [];
    for (const group of config.assetGroups ?? []) {
      patterns.push(...(group.resources?.files ?? []), ...(group.resources?.urls ?? []));
    }
    patterns.push(...(config.navigationUrls ?? []));

    const apiHits = patterns.filter((p) => /\/api\b/.test(p) && !p.startsWith('!'));
    expect(apiHits, `API-caching patterns: ${apiHits.join(', ')}`).toEqual([]);

    const nav = config.navigationUrls ?? [];
    expect(nav.some((u) => u === '!/api/**' || u.startsWith('!/api'))).toBe(true);
  });
});
