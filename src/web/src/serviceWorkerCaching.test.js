import { readFileSync } from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';

const loadServiceWorker = () => {
  const source = readFileSync(
    path.resolve(process.cwd(), 'public/service-worker.js'),
    'utf8',
  );
  const listeners = new Map();
  const cache = {
    addAll: vi.fn().mockResolvedValue(undefined),
  };
  const context = {
    URL,
    caches: {
      delete: vi.fn().mockResolvedValue(true),
      keys: vi.fn().mockResolvedValue(['slskdn-shell-v1']),
      match: vi.fn().mockResolvedValue(undefined),
      open: vi.fn().mockResolvedValue(cache),
    },
    fetch: vi.fn(async (request) => ({ ok: true, request })),
    self: {
      addEventListener: (type, handler) => {
        listeners.set(type, handler);
      },
      clients: {
        claim: vi.fn(),
      },
      skipWaiting: vi.fn(),
    },
  };

  vm.runInNewContext(source, context, {
    filename: 'service-worker.js',
  });

  return { cache, context, listeners };
};

describe('service worker caching', () => {
  it('precaches only the static shell assets', async () => {
    const { cache, context, listeners } = loadServiceWorker();
    let pending;

    listeners.get('install')({
      waitUntil: (promise) => {
        pending = promise;
      },
    });

    await pending;

    expect(context.caches.open).toHaveBeenCalledWith('slskdn-shell-v2');
    expect(cache.addAll).toHaveBeenCalledWith([
      './manifest.json',
      './logo192.png',
      './logo512.png',
    ]);
    expect(context.self.skipWaiting).toHaveBeenCalled();
  });

  it('uses network-first fetches for navigation requests', async () => {
    const { context, listeners } = loadServiceWorker();
    const request = {
      destination: 'document',
      headers: {
        get: () => 'text/html',
      },
      method: 'GET',
      mode: 'navigate',
      url: 'http://localhost/system',
    };
    const respondWith = vi.fn();

    listeners.get('fetch')({
      request,
      respondWith,
    });

    await respondWith.mock.calls[0][0];

    expect(context.fetch).toHaveBeenCalledWith(request);
    expect(context.caches.match).not.toHaveBeenCalled();
  });

  it('does not serve hashed asset requests from the cache', async () => {
    const { context, listeners } = loadServiceWorker();
    const request = {
      destination: 'script',
      headers: {
        get: () => 'application/javascript',
      },
      method: 'GET',
      mode: 'cors',
      url: 'http://localhost/assets/index-abc123.js',
    };
    const respondWith = vi.fn();

    listeners.get('fetch')({
      request,
      respondWith,
    });

    await respondWith.mock.calls[0][0];

    expect(context.fetch).toHaveBeenCalledWith(request);
    expect(context.caches.match).not.toHaveBeenCalled();
  });
});
