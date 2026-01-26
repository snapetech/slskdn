import { Page } from '@playwright/test';
import * as net from 'net';

// Note: fetch is available in Node 18+, but TypeScript may need types
declare const fetch: typeof globalThis.fetch;

/**
 * Find a free port on localhost.
 */
export async function findFreePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.listen(0, () => {
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
  await page.goto(`${apiUrl}/`);
  
  // Wait for login form (or redirect if already logged in)
  try {
    await page.waitForSelector('[data-testid="login-username"], [data-testid="nav-contacts"]', { timeout: 5000 });
    
    // Check if already logged in
    if (await page.locator('[data-testid="nav-contacts"]').isVisible()) {
      return;
    }
    
    // Fill login form
    await page.fill('[data-testid="login-username"]', username);
    await page.fill('[data-testid="login-password"]', password);
    await page.click('[data-testid="login-submit"]');
    
    // Wait for navigation to main app
    await page.waitForURL(`${apiUrl}/*`, { timeout: 10000 });
  } catch (err) {
    // If login form not found, might already be logged in or different UI
    // Check if we're on a protected page
    const currentUrl = page.url();
    if (currentUrl.includes(apiUrl) && !currentUrl.includes('/login')) {
      return; // Assume already logged in
    }
    throw err;
  }
}

/**
 * Extract invite link from the create invite modal.
 */
export async function getInviteLink(page: Page): Promise<string> {
  await page.waitForSelector('[data-testid="invite-link"]', { timeout: 5000 });
  const link = await page.textContent('[data-testid="invite-link"]');
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
