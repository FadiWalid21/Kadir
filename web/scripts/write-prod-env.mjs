#!/usr/bin/env node
/**
 * Writes src/environments/environment.prod.ts from API_BASE_URL (Vercel / CI).
 * Accepts either https://host or https://host/api/v1 — normalizes to .../api/v1.
 * Fails the build when unset so demo deploys cannot ship a relative /api/v1 base.
 */
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const outPath = join(__dirname, '../src/environments/environment.prod.ts');

const raw = (process.env.API_BASE_URL || '').trim().replace(/\/+$/, '');
if (!raw) {
  console.error(
    'API_BASE_URL is required for production builds (e.g. https://kadir.runasp.net). ' +
      'Set it in Vercel Project → Settings → Environment Variables (Production), then redeploy.',
  );
  process.exit(1);
}

const apiBaseUrl = raw.endsWith('/api/v1') ? raw : `${raw}/api/v1`;

const contents = `export const environment = {
  production: true,
  apiBaseUrl: ${JSON.stringify(apiBaseUrl)},
};
`;

writeFileSync(outPath, contents, 'utf8');
console.log(`Wrote environment.prod.ts apiBaseUrl=${apiBaseUrl}`);
