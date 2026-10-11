/**
 * TEMP PWA icons from logo-kadr.png (burgundy canvas, mark in 80% safe zone).
 * Replace with exports from icon-master.png when available.
 * Dev-only: requires sharp (devDependency).
 */
import { mkdirSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');
const brandDir = join(root, 'public', 'brand');
const publicDir = join(root, 'public');
const logoPath = join(brandDir, 'logo-kadr.png');
const masterPath = join(brandDir, 'icon-master.png');
const BURGUNDY = { r: 0x7a, g: 0x1f, b: 0x3d, alpha: 1 };

async function composeIcon(size, { maskable = false } = {}) {
  const source = existsSync(masterPath) ? masterPath : logoPath;
  const safe = Math.round(size * 0.8);
  const mark = await sharp(source)
    .resize(safe, safe, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toBuffer();

  const left = Math.round((size - safe) / 2);
  const top = left;
  return sharp({
    create: {
      width: size,
      height: size,
      channels: 4,
      background: BURGUNDY,
    },
  })
    .composite([{ input: mark, left, top }])
    .png()
    .toBuffer();
}

async function main() {
  mkdirSync(brandDir, { recursive: true });
  const temp = !existsSync(masterPath);
  if (temp) {
    console.log('TEMP: icon-master.png missing — generating from logo-kadr.png');
  }

  const icon192 = await composeIcon(192);
  const icon512 = await composeIcon(512);
  const iconMaskable = await composeIcon(512, { maskable: true });
  const apple180 = await composeIcon(180);

  await sharp(icon192).toFile(join(brandDir, 'icon-192.png'));
  await sharp(icon512).toFile(join(brandDir, 'icon-512.png'));
  await sharp(iconMaskable).toFile(join(brandDir, 'icon-512-maskable.png'));
  await sharp(apple180).toFile(join(brandDir, 'apple-touch-icon.png'));

  // Favicon set (ICO via 32 PNG is enough for modern browsers as favicon.png + ico).
  await sharp(await composeIcon(32)).toFile(join(publicDir, 'favicon-32.png'));
  await sharp(await composeIcon(16)).toFile(join(publicDir, 'favicon-16.png'));
  await sharp(await composeIcon(48)).png().toFile(join(publicDir, 'favicon.ico'));

  // Shell logo: real display ~40–56 CSS px → 128px asset + WebP.
  const logoSm = await sharp(logoPath)
    .resize(128, 128, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toBuffer();
  await sharp(logoSm).toFile(join(brandDir, 'logo-kadr-128.png'));
  await sharp(logoSm).webp({ quality: 82 }).toFile(join(brandDir, 'logo-kadr-128.webp'));

  const before = (await sharp(logoPath).metadata()).size;
  console.log(
    JSON.stringify(
      {
        tempFromLogo: temp,
        sourceBytes: before,
        outputs: [
          'brand/icon-192.png',
          'brand/icon-512.png',
          'brand/icon-512-maskable.png',
          'brand/apple-touch-icon.png',
          'brand/logo-kadr-128.png',
          'brand/logo-kadr-128.webp',
          'favicon-32.png',
          'favicon-16.png',
          'favicon.ico',
        ],
      },
      null,
      2,
    ),
  );
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
