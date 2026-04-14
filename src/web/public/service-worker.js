const CACHE_NAME = 'slskdn-shell-v2';
const APP_SHELL_ASSETS = ['./manifest.json', './logo192.png', './logo512.png'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(APP_SHELL_ASSETS)),
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.map((key) => caches.delete(key))),
    ),
  );
  self.clients.claim();
});

// Strategy:
// - Navigation / HTML requests: NETWORK-FIRST (never serve a cached index.html).
//   A stale index.html references hashed asset bundles that no longer exist
//   after a rebuild, which produces a blank page and 404s on /assets/*.
// - Hashed asset requests (under /assets/): network-only, no caching. Vite
//   fingerprints filenames, so the browser's HTTP cache already handles this
//   and a SW miss on a removed hash must NOT fall through to a stale index.
// - Other GETs (manifest, icons): cache-first against the pre-cached shell.
self.addEventListener('fetch', (event) => {
  const request = event.request;

  if (request.method !== 'GET') {
    return;
  }

  const url = new URL(request.url);
  const isNavigation =
    request.mode === 'navigate' ||
    (request.destination === 'document') ||
    (request.headers.get('accept') || '').includes('text/html');

  if (isNavigation) {
    event.respondWith(fetch(request));
    return;
  }

  if (url.pathname.includes('/assets/')) {
    event.respondWith(fetch(request));
    return;
  }

  event.respondWith(
    caches.match(request).then((cachedResponse) => {
      if (cachedResponse) {
        return cachedResponse;
      }
      return fetch(request);
    }),
  );
});
