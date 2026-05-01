import fs from 'fs';
import http from 'http';
import path from 'path';
import { fileURLToPath } from 'url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const buildDir = path.resolve(scriptDir, '..', 'build');
const indexPath = path.join(buildDir, 'index.html');
const mountPath = '/slskd/';
const deepLinkPath = '/slskd/system/info';

const contentTypes = new Map([
  ['.css', 'text/css; charset=utf-8'],
  ['.html', 'text/html; charset=utf-8'],
  ['.ico', 'image/x-icon'],
  ['.js', 'text/javascript; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'],
  ['.map', 'application/json; charset=utf-8'],
  ['.png', 'image/png'],
  ['.svg', 'image/svg+xml; charset=utf-8'],
  ['.txt', 'text/plain; charset=utf-8'],
  ['.webmanifest', 'application/manifest+json; charset=utf-8'],
]);

function fail(message) {
  console.error(`ERROR: ${message}`);
  process.exit(1);
}

function getContentType(filePath) {
  return contentTypes.get(path.extname(filePath).toLowerCase()) ?? 'application/octet-stream';
}

function normalizeRequestPath(requestUrl) {
  const pathname = new URL(requestUrl, 'http://127.0.0.1').pathname;

  if (pathname === '/slskd') {
    return mountPath;
  }

  return pathname;
}

function resolveFilePath(requestPath) {
  if (!requestPath.startsWith(mountPath)) {
    return null;
  }

  const relativePath = requestPath === mountPath
    ? 'index.html'
    : requestPath.slice(mountPath.length);
  const normalizedRelativePath = path.posix.normalize(relativePath).replace(/^(\.\.(\/|\\|$))+/, '');
  const filePath = path.resolve(buildDir, normalizedRelativePath);

  if (!filePath.startsWith(buildDir)) {
    return null;
  }

  return filePath;
}

function injectBaseHref(html) {
  return html.replace('<head>', `<head><base href="${mountPath}" />`);
}

if (!fs.existsSync(indexPath)) {
  fail(`Missing built index.html at ${indexPath}`);
}

const server = http.createServer((req, res) => {
  const requestPath = normalizeRequestPath(req.url ?? mountPath);
  const filePath = resolveFilePath(requestPath);

  const extension = path.extname(requestPath);

  if ((!filePath || !fs.existsSync(filePath)) && !extension && requestPath.startsWith(mountPath)) {
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    res.end(injectBaseHref(fs.readFileSync(indexPath, 'utf8')));
    return;
  }

  if (!filePath || !fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) {
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
    res.end('Not Found');
    return;
  }

  if (filePath === indexPath) {
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    res.end(injectBaseHref(fs.readFileSync(filePath, 'utf8')));
    return;
  }

  res.writeHead(200, { 'Content-Type': getContentType(filePath) });
  fs.createReadStream(filePath).pipe(res);
});

server.listen(0, '127.0.0.1', async () => {
  const address = server.address();

  if (!address || typeof address === 'string') {
    fail('Failed to bind subpath smoke test server');
  }

  const origin = `http://127.0.0.1:${address.port}`;
  const baseUrl = `${origin}${deepLinkPath}`;

  try {
    const indexResponse = await fetch(baseUrl);

    if (!indexResponse.ok) {
      fail(`Expected ${baseUrl} to return 200, got ${indexResponse.status}`);
    }

    const indexHtml = await indexResponse.text();
    if (/(?:src|href)="\/assets\//.test(indexHtml)) {
      fail('Expected built index.html to avoid root-relative /assets references');
    }

    if (!indexHtml.includes(`<base href="${mountPath}" />`)) {
      fail('Expected served index.html to inject a mount base href for deep-link asset resolution');
    }

    const assetMatches = [...indexHtml.matchAll(/(?:src|href)="(\.\/assets\/[^"]+)"/g)];
    const assetPaths = [...new Set(assetMatches.map((match) => match[1]))];

    if (assetPaths.length === 0) {
      fail('Expected built index.html to expose relative asset references under a subpath');
    }

    for (const assetPath of assetPaths) {
      const assetUrl = new URL(assetPath, `${origin}${mountPath}`);
      const assetResponse = await fetch(assetUrl);

      if (!assetResponse.ok) {
        fail(`Expected ${assetUrl} to return 200, got ${assetResponse.status}`);
      }

      const contentType = assetResponse.headers.get('content-type') ?? '';

      if (assetPath.endsWith('.js') && contentType.includes('text/html')) {
        fail(`Expected ${assetUrl} to resolve to JavaScript, got ${contentType}`);
      }

      if (assetPath.endsWith('.css') && contentType.includes('text/html')) {
        fail(`Expected ${assetUrl} to resolve to CSS, got ${contentType}`);
      }
    }

    console.log('Verified built web output loads correctly from a deep link under /slskd/ with relative assets and a mounted base href.');
  } catch (error) {
    fail(error instanceof Error ? error.message : String(error));
  } finally {
    server.close();
  }
});
