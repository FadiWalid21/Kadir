#!/usr/bin/env node
/**
 * Fails when app source uses hard-coded colors/fonts, arbitrary Tailwind values,
 * or physical direction utilities. Allowlist: tokens.css, fonts.css.
 */
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const srcRoot = path.join(root, 'src');

const ALLOWED = new Set([
  path.join(srcRoot, 'styles', 'tokens.css'),
  path.join(srcRoot, 'styles', 'fonts.css'),
]);

const EXTENSIONS = new Set(['.ts', '.html', '.css']);

const RULES = [
  {
    name: 'hard-coded-hex',
    // Hex colors outside allowlisted token files
    pattern: /#(?:[0-9a-fA-F]{3,8})\b/g,
  },
  {
    name: 'hard-coded-rgb',
    pattern: /\brgba?\s*\(/g,
  },
  {
    name: 'font-family-literal',
    pattern: /font-family\s*:/gi,
  },
  {
    name: 'arbitrary-tailwind-color',
    pattern: /\b(?:bg|text|border|from|to|via|ring|fill|stroke)-\[#[^\]]+\]/g,
  },
  {
    name: 'arbitrary-tailwind-size',
    pattern: /\b(?:text|p|px|py|pt|pb|ps|pe|m|mx|my|mt|mb|ms|me|gap|w|h|min-w|min-h|max-w|max-h)-\[\d+(?:\.\d+)?(?:px|rem|em)\]/g,
  },
  {
    name: 'arbitrary-font-family',
    pattern: /\bfont-\[[^\]]+\]/g,
  },
  {
    name: 'physical-direction-utility',
    pattern: /(?:^|[\s"'`])(?:ml|mr|pl|pr|left|right)-/g,
  },
];

async function walk(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await walk(full)));
    } else if (EXTENSIONS.has(path.extname(entry.name))) {
      files.push(full);
    }
  }
  return files;
}

const issues = [];
const files = await walk(srcRoot);

for (const file of files) {
  if (ALLOWED.has(file)) {
    continue;
  }
  const content = await readFile(file, 'utf8');
  const relative = path.relative(root, file);

  for (const rule of RULES) {
    // brand-color.ts and theme tests need hex literals for contrast math
    if (
      (rule.name === 'hard-coded-hex' || rule.name === 'hard-coded-rgb') &&
      (file.endsWith(`${path.sep}brand-color.ts`) ||
        file.endsWith(`${path.sep}brand-color.spec.ts`) ||
        file.endsWith(`${path.sep}theme.service.spec.ts`))
    ) {
      continue;
    }
    if (rule.name === 'font-family-literal') {
      if (!file.endsWith('.css') || file.endsWith(`${path.sep}base.css`)) {
        // base.css applies var(--font-body); raw stacks only in fonts.css
        continue;
      }
    }
    const matches = content.match(rule.pattern);
    if (matches?.length) {
      issues.push(`${relative}: ${rule.name} (${matches.length} hit(s)) e.g. "${matches[0].trim()}"`);
    }
  }
}

if (issues.length) {
  console.error('Design token lint failed:\n' + issues.map((i) => `  - ${i}`).join('\n'));
  process.exit(1);
}

console.log('Design token lint passed.');
