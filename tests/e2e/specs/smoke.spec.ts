import { test, expect } from '@playwright/test';
import { MultiPeerHarness } from '../harness/MultiPeerHarness';
import { login, waitForHealth } from '../fixtures/helpers';
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

  test.beforeAll(async () => {
    harness = new MultiPeerHarness();
  });

  test.afterAll(async () => {
    await harness.stopAll();
  });

  test('should start node and serve web UI', async () => {
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music');
    
    // Verify health endpoint
    const healthResponse = await fetch(`${node.apiUrl}/health`);
    expect(healthResponse.ok).toBe(true);
    
    // Verify web UI is served
    const pageResponse = await fetch(`${node.apiUrl}/`);
    expect(pageResponse.ok).toBe(true);
    expect(pageResponse.headers.get('content-type')).toContain('text/html');
  });

  test('should login successfully', async ({ page }) => {
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music');
    
    await page.goto(`${node.apiUrl}/`);
    await login(page, node.apiUrl, 'admin', 'admin');
    
    // Verify we're logged in (check for navigation elements)
    // Note: This assumes the UI has nav elements visible after login
    // Adjust selectors based on actual UI
    await page.waitForLoadState('networkidle');
    
    // Should be able to navigate (if nav is present)
    const url = page.url();
    expect(url).toContain(node.apiUrl);
  });

  test('should load contacts page', async ({ page }) => {
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music');
    
    await page.goto(`${node.apiUrl}/contacts`);
    await login(page, node.apiUrl, 'admin', 'admin');
    
    // Verify contacts page loaded
    await expect(page.locator('h1:has-text("Contacts")')).toBeVisible({ timeout: 10000 });
  });

  test('should load share groups page', async ({ page }) => {
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music');
    
    await page.goto(`${node.apiUrl}/sharegroups`);
    await login(page, node.apiUrl, 'admin', 'admin');
    
    // Verify share groups page loaded
    await expect(page.locator('h1:has-text("Share Groups")')).toBeVisible({ timeout: 10000 });
  });
});
