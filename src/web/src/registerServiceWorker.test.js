// <copyright file="registerServiceWorker.test.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

const loadModule = async (urlBase) => {
  vi.resetModules();
  vi.doMock('./config', () => ({
    urlBase,
  }));

  return import('./registerServiceWorker');
};

describe('registerServiceWorker', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('registers the worker immediately when the document is already loaded', async () => {
    const register = vi.fn().mockResolvedValue({});
    Object.defineProperty(document, 'readyState', {
      configurable: true,
      value: 'complete',
    });
    Object.defineProperty(globalThis, 'navigator', {
      configurable: true,
      value: { serviceWorker: { register } },
    });

    const { registerServiceWorker } = await loadModule('/system');
    registerServiceWorker();

    expect(register).toHaveBeenCalledWith('/system/service-worker.js', {
      scope: '/system/',
    });
  });

  it('waits for window load when the document is still loading', async () => {
    const addEventListener = vi.spyOn(window, 'addEventListener');
    const register = vi.fn().mockResolvedValue({});
    Object.defineProperty(document, 'readyState', {
      configurable: true,
      value: 'loading',
    });
    Object.defineProperty(globalThis, 'navigator', {
      configurable: true,
      value: { serviceWorker: { register } },
    });

    const { registerServiceWorker } = await loadModule('');
    registerServiceWorker();

    expect(addEventListener).toHaveBeenCalledWith(
      'load',
      expect.any(Function),
      { once: true },
    );

    addEventListener.mockRestore();
  });
});
