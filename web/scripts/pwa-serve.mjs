#!/usr/bin/env node
/**
 * Production build + static server with SPA fallback and /api proxy to the local API.
 * Zero dependencies (Node built-ins only). Usage: npm run pwa:serve
 */
import { spawn } from 'node:child_process';
import { createReadStream, existsSync, statSync } from 'node:fs';
import { createServer, request as httpRequest } from 'node:http';
import { extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = fileURLToPath(new URL('.', import.meta.url));
const webRoot = resolve(__dirname, '..');
const distRoot = join(webRoot, 'dist', 'web', 'browser');
const port = Number(process.env.PWA_PORT || 4173);
const apiTarget = new URL(process.env.PWA_API || 'http://127.0.0.1:5248');

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.webmanifest': 'application/manifest+json; charset=utf-8',
  '.png': 'image/png',
  '.webp': 'image/webp',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
  '.woff': 'font/woff',
  '.map': 'application/json',
};

function runBuild() {
  return new Promise((resolvePromise, reject) => {
    const child = spawn('npm', ['run', 'build'], {
      cwd: webRoot,
      stdio: 'inherit',
      shell: process.platform === 'win32',
    });
    child.on('exit', (code) => {
      if (code === 0) {
        resolvePromise();
      } else {
        reject(new Error(`build exited with ${code}`));
      }
    });
  });
}

function sendFile(res, filePath) {
  const ext = extname(filePath).toLowerCase();
  const type = MIME[ext] || 'application/octet-stream';
  const base = filePath.split(/[/\\]/).pop() ?? '';
  const noCache =
    base === 'index.html' ||
    base === 'ngsw-worker.js' ||
    base === 'ngsw.json' ||
    base === 'manifest.webmanifest';
  const immutable = !noCache && (ext === '.js' || ext === '.css');

  res.writeHead(200, {
    'Content-Type': type,
    'Cache-Control': noCache
      ? 'no-cache'
      : immutable
        ? 'public, max-age=31536000, immutable'
        : 'public, max-age=3600',
  });
  createReadStream(filePath).pipe(res);
}

function proxyApi(req, res) {
  const headers = { ...req.headers, host: apiTarget.host };
  const opts = {
    protocol: apiTarget.protocol,
    hostname: apiTarget.hostname,
    port: apiTarget.port || (apiTarget.protocol === 'https:' ? 443 : 80),
    path: req.url,
    method: req.method,
    headers,
  };
  const upstream = httpRequest(opts, (proxyRes) => {
    res.writeHead(proxyRes.statusCode ?? 502, proxyRes.headers);
    proxyRes.pipe(res);
  });
  upstream.on('error', (err) => {
    res.writeHead(502, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end(`API proxy error: ${err.message}`);
  });
  req.pipe(upstream);
}

function resolvePath(urlPath) {
  const decoded = decodeURIComponent(urlPath.split('?')[0] || '/');
  const safe = normalize(decoded).replace(/^(\.\.[/\\])+/, '');
  const candidate = join(distRoot, safe);
  if (!candidate.startsWith(distRoot)) {
    return join(distRoot, 'index.html');
  }
  if (existsSync(candidate) && statSync(candidate).isFile()) {
    return candidate;
  }
  if (existsSync(candidate) && statSync(candidate).isDirectory()) {
    const indexInDir = join(candidate, 'index.html');
    if (existsSync(indexInDir)) {
      return indexInDir;
    }
  }
  // SPA fallback: only for extension-less routes (never invent missing assets).
  if (!extname(safe) || safe.endsWith('/')) {
    return join(distRoot, 'index.html');
  }
  return null;
}

function startServer() {
  if (!existsSync(join(distRoot, 'index.html'))) {
    throw new Error(`Missing ${distRoot}/index.html — build failed?`);
  }

  createServer((req, res) => {
    const url = req.url ?? '/';
    if (url.startsWith('/api')) {
      proxyApi(req, res);
      return;
    }
    const filePath = resolvePath(url);
    if (!filePath || !existsSync(filePath)) {
      res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
      res.end('Not found');
      return;
    }
    sendFile(res, filePath);
  }).listen(port, () => {
    console.log(`PWA prod server: http://localhost:${port}`);
    console.log(`Proxying /api → ${apiTarget.origin}`);
    console.log(`Serving ${distRoot}`);
  });
}

const skipBuild = process.argv.includes('--no-build');

try {
  if (!skipBuild) {
    await runBuild();
  }
  startServer();
} catch (err) {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
}
