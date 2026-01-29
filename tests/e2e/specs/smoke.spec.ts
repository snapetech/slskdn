import { test, expect } from '@playwright/test';
import { MultiPeerHarness } from '../harness/MultiPeerHarness';
import { login, waitForAppReady, waitForHealth } from '../fixtures/helpers';
import { selectors } from '../fixtures/selectors';

/**
 * Smoke tests: Basic functionality verification.
 * 
 * These tests verify that:
 * - Nodes can start and serve the web UI
 * - Login works
 * - Basic pages load
 * - Navigation works
 */
test.describe('Smoke Tests', () => {
  let harness: MultiPeerHarness;
  let nodeApiUrl: string;

  // Node startup can take 60–90s (port allocation, dotnet cold start, health check). Use 2m so we don't flake.
  test.beforeAll(async () => {
    harness = new MultiPeerHarness();

    // One node for the whole suite.
    // Starting/stopping a node per test is slow and can mask shared-state issues.
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music');
    nodeApiUrl = node.apiUrl;
    await waitForHealth(nodeApiUrl);
  }, { timeout: 120000 });

  test.afterAll(async () => {
    await harness.stopAll();
  });

  test('should start node and serve web UI', async () => {
    // Verify health endpoint
    const healthResponse = await fetch(`${nodeApiUrl}/health`);
    expect(healthResponse.ok).toBe(true);
    
    // Verify web UI is served
    const pageResponse = await fetch(`${nodeApiUrl}/`);
    expect(pageResponse.ok).toBe(true);
    expect(pageResponse.headers.get('content-type')).toContain('text/html');
  });

  test('should login successfully', async ({ page }) => {
    await login(page, nodeApiUrl, 'admin', 'admin');
    await waitForAppReady(page, nodeApiUrl);

    await expect(page.locator(selectors.nav.contacts)).toBeVisible();

    const url = page.url();
    expect(url).toContain(nodeApiUrl);
  });

  test('should load contacts page', async ({ page }) => {
    await login(page, nodeApiUrl, 'admin', 'admin');
    await waitForAppReady(page, nodeApiUrl);

    await page.goto(`${nodeApiUrl}/contacts`);
    await expect(page.locator(selectors.contacts.createInvite)).toBeVisible();
  });

  test('should load share groups page', async ({ page }) => {
    await login(page, nodeApiUrl, 'admin', 'admin');
    await waitForAppReady(page, nodeApiUrl);

    await page.goto(`${nodeApiUrl}/sharegroups`);
    await expect(page.locator(selectors.shareGroups.createGroup)).toBeVisible();
  });
});
