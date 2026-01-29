import { Page } from '@playwright/test';
import * as net from 'net';

// Note: fetch is available in Node 18+, but TypeScript may need types
declare const fetch: typeof globalThis.fetch;

/**
 * Find a free port on localhost.
 * Uses reuseAddress so the port can be rebound immediately after we close the probe server (avoids TIME_WAIT).
 */
export async function findFreePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.listen({ port: 0, host: '127.0.0.1', reuseAddress: true }, () => {
      const addr = server.address();
      if (addr && typeof addr === 'object') {
        const port = addr.port;
        server.close(() => resolve(port));
      } else {
        reject(new Error('Failed to find free port'));
      }
    });
    server.on('error', reject);
  });
}

/**
 * Wait for a node's health endpoint to respond.
 */
export async function waitForHealth(apiUrl: string, timeout = 30000): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeout) {
    try {
      const response = await fetch(`${apiUrl}/health`);
      if (response.ok) {
        return;
      }
    } catch (err) {
      // Ignore errors, keep polling
    }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error(`Health check timeout for ${apiUrl} after ${timeout}ms`);
}

/**
 * Login to a slskdn instance via the web UI.
 */
export async function login(page: Page, apiUrl: string, username: string, password: string): Promise<void> {
  const navContacts = '[data-testid="nav-contacts"]';
  const usernameSelector =
    '[data-testid="login-username"] input, input[placeholder="Username"], input[name="username"]';
  const passwordSelector =
    '[data-testid="login-password"] input, input[placeholder="Password"], input[name="password"], input[type="password"]';
  const submitSelector = '[data-testid="login-submit"], button:has-text("Login")';
  const lostConnectionSelector = 'text=Lost connection to slskd';

  // Sometimes the app can boot into a transient "Lost connection" state while SignalR is negotiating.
  // For E2E, auto-retry once by reloading.
  for (let attempt = 0; attempt < 2; attempt++) {
    await page.goto(`${apiUrl}/`);

    if (await page.locator(lostConnectionSelector).isVisible().catch(() => false)) {
      await page.reload();
    }

    // Wait for either an already-logged-in state or a login form.
    await page.waitForSelector(`${navContacts}, ${usernameSelector}`, { timeout: 30000 });

    // Already logged in.
    if (await page.locator(navContacts).isVisible().catch(() => false)) {
      return;
    }

    // Fill login form.
    await page.fill(usernameSelector, username);
    await page.fill(passwordSelector, password);
    await page.click(submitSelector);

    try {
      // Wait for the app chrome to appear.
      // Startup can be a bit slow on cold runs (SignalR hub + initial state fetch).
      await page.waitForSelector(navContacts, { timeout: 30000 });
      return;
    } catch (error) {
      const lostConnection = await page
        .locator(lostConnectionSelector)
        .isVisible()
        .catch(() => false);
      if (lostConnection && attempt === 0) {
        await page.reload();
        continue;
      }
      throw error;
    }
  }
}

/**
 * Wait for the main app to be ready after login.
 * Waits for either the nav to be visible or the initial Loader to disappear
 * (app may show Loader until hub connects; if hub never connects we wait for Loader to be hidden).
 */
export async function waitForAppReady(page: Page, _apiUrl: string, timeout = 20000): Promise<void> {
  try {
    await page.waitForSelector(
      '[data-testid="nav-contacts"], [data-testid="nav-search"], [data-testid="nav-solid"]',
      { timeout }
    );
    return;
  } catch {
    // Nav never appeared; wait for the big initial Loader to disappear (init finished or failed)
    await page
      .locator('.ui.active.loader')
      .first()
      .waitFor({ state: 'hidden', timeout: 10000 })
      .catch(() => {});
  }
}

/**
 * Extract invite link from the create invite modal.
 */
export async function getInviteLink(page: Page): Promise<string> {
  await page.waitForSelector('[data-testid="contacts-invite-output"]', { timeout: 5000 });
  const link = await page.getAttribute('[data-testid="contacts-invite-output"]', 'value');
  if (!link) {
    throw new Error('Invite link not found');
  }
  return link.trim();
}

/**
 * Wait for API response with retry logic.
 */
export async function waitForApiResponse(
  apiUrl: string,
  path: string,
  expectedStatus: number = 200,
  timeout = 10000
): Promise<Response> {
  const start = Date.now();
  while (Date.now() - start < timeout) {
    try {
      const response = await fetch(`${apiUrl}${path}`);
      if (response.status === expectedStatus) {
        return response;
      }
    } catch (err) {
      // Ignore errors, keep polling
    }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error(`API response timeout for ${apiUrl}${path} (expected ${expectedStatus})`);
}
