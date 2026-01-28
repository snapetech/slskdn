import { test, expect } from '@playwright/test';
import { MultiPeerHarness } from '../harness/MultiPeerHarness';
import { FakeSolidServer } from '../harness/FakeSolidServer';
import { login } from '../fixtures/helpers';

/**
 * Solid integration E2E tests.
 * 
 * These tests verify:
 * - Solid feature is accessible when enabled
 * - WebID resolution works with SSRF hardening
 * - UI displays resolved OIDC issuers
 * - Client ID document is served correctly
 */
test.describe('Solid Integration', () => {
  let harness: MultiPeerHarness;
  let fakeSolid: FakeSolidServer;

  test.beforeAll(async () => {
    harness = new MultiPeerHarness();
    
    // Start fake Solid server
    fakeSolid = new FakeSolidServer();
    await fakeSolid.start();
  });

  test.afterAll(async () => {
    await harness.stopAll();
    await fakeSolid.stop();
  });

  test('should load Solid settings page when feature is enabled', async ({ page }) => {
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: true,
      solidAllowedHosts: [fakeSolid.getHostname()]
    });

    await page.goto(`${node.apiUrl}/solid`);
    await login(page, node.apiUrl, 'admin', 'admin');

    // Verify Solid page loaded
    await expect(page.locator('h2:has-text("Solid")')).toBeVisible({ timeout: 10000 });
    
    // Verify status message shows feature is enabled
    await expect(page.locator('text=Client ID:')).toBeVisible({ timeout: 5000 });
  });

  test('should resolve WebID and display OIDC issuers', async ({ page }) => {
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: true,
      solidAllowedHosts: [fakeSolid.getHostname()]
    });

    await page.goto(`${node.apiUrl}/solid`);
    await login(page, node.apiUrl, 'admin', 'admin');

    // Wait for page to load
    await expect(page.locator('h2:has-text("Solid")')).toBeVisible({ timeout: 10000 });

    // Enter WebID
    const webIdInput = page.locator('[data-testid="solid-webid-input"]').or(page.locator('input[placeholder*="WebID"]'));
    await webIdInput.fill(fakeSolid.getWebIdUrl());

    // Click resolve button
    const resolveButton = page.locator('[data-testid="solid-resolve-webid"]');
    await resolveButton.click();

    // Wait for resolved data to appear
    await expect(page.locator('text=oidcIssuers')).toBeVisible({ timeout: 10000 });
    
    // Verify issuer URL is displayed (should contain the fake server's OIDC endpoint)
    await expect(page.locator(`text=${fakeSolid.getBaseUrl()}/oidc`)).toBeVisible({ timeout: 5000 });
  });

  test('should serve Client ID document at correct endpoint', async ({ page }) => {
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: true,
      solidAllowedHosts: [fakeSolid.getHostname()]
    });

    await login(page, node.apiUrl, 'admin', 'admin');

    // Fetch Client ID document directly
    const response = await page.request.get(`${node.apiUrl}/solid/clientid.jsonld`);
    
    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toContain('application/ld+json');
    
    const doc = await response.json();
    expect(doc['@context']).toBe('https://www.w3.org/ns/solid/oidc-context.jsonld');
    expect(doc.client_id).toContain('/solid/clientid.jsonld');
    expect(doc.redirect_uris).toBeDefined();
    expect(Array.isArray(doc.redirect_uris)).toBe(true);
  });

  test('should return 404 when Solid feature is disabled', async ({ page }) => {
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: false
    });

    await login(page, node.apiUrl, 'admin', 'admin');

    // Try to access Solid endpoint
    const response = await page.request.get(`${node.apiUrl}/api/v0/solid/status`);
    expect(response.status()).toBe(404);

    // Try to access Client ID document
    const clientIdResponse = await page.request.get(`${node.apiUrl}/solid/clientid.jsonld`);
    expect(clientIdResponse.status()).toBe(404);
  });

  test('should block WebID resolution when host not in AllowedHosts', async ({ page }) => {
    const node = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: true,
      solidAllowedHosts: [] // Empty = deny all
    });

    await page.goto(`${node.apiUrl}/solid`);
    await login(page, node.apiUrl, 'admin', 'admin');

    await expect(page.locator('h2:has-text("Solid")')).toBeVisible({ timeout: 10000 });

    // Try to resolve WebID (should fail due to SSRF policy)
    const webIdInput = page.locator('[data-testid="solid-webid-input"]').or(page.locator('input[placeholder*="WebID"]'));
    await webIdInput.fill(fakeSolid.getWebIdUrl());

    const resolveButton = page.locator('[data-testid="solid-resolve-webid"]');
    await resolveButton.click();

    // Should show error message
    await expect(page.locator('text=blocked').or(page.locator('text=error').or(page.locator('text=denied')))).toBeVisible({ timeout: 10000 });
  });
});
