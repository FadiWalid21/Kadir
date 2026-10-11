import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import sharp from 'sharp';

const webRoot = join(process.cwd());

describe('manifest.webmanifest', () => {
  const manifestPath = join(webRoot, 'public/manifest.webmanifest');
  const tokensPath = join(webRoot, 'src/styles/tokens.css');

  it('has required PWA fields and icons with declared dimensions', async () => {
    const manifest = JSON.parse(readFileSync(manifestPath, 'utf8')) as {
      name: string;
      short_name: string;
      lang: string;
      dir: string;
      start_url: string;
      scope: string;
      display: string;
      background_color: string;
      theme_color: string;
      icons: { src: string; sizes: string; type: string; purpose?: string }[];
    };

    expect(manifest.name).toBe('كادر | Kadr');
    expect(manifest.short_name).toBe('كادر');
    expect(manifest.lang).toBe('ar');
    expect(manifest.dir).toBe('rtl');
    expect(manifest.start_url).toBe('/');
    expect(manifest.scope).toBe('/');
    expect(manifest.display).toBe('standalone');

    const tokens = readFileSync(tokensPath, 'utf8');
    const bgApp = tokens.match(/:root\s*\{[^}]*--bg-app:\s*(#[0-9A-Fa-f]{6})/)?.[1];
    const brand = tokens.match(/:root\s*\{[^}]*--brand-base:\s*(#[0-9A-Fa-f]{6})/)?.[1];
    expect(manifest.background_color.toUpperCase()).toBe(bgApp!.toUpperCase());
    expect(manifest.theme_color.toUpperCase()).toBe(brand!.toUpperCase());

    expect(manifest.icons.some((i) => i.purpose === 'maskable')).toBe(true);

    for (const icon of manifest.icons) {
      const filePath = join(webRoot, 'public', icon.src.replace(/^\//, ''));
      expect(existsSync(filePath), `missing ${icon.src}`).toBe(true);
      const [w, h] = icon.sizes.split('x').map(Number);
      const meta = await sharp(filePath).metadata();
      expect(meta.width).toBe(w);
      expect(meta.height).toBe(h);
    }
  });
});
