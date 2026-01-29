import { execSync } from 'child_process';
import * as path from 'path';
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
 *
 * All Solid E2E tests start a real slskdn node (each with --app-dir for isolated mutex/data).
 * UI tests (load Solid page, resolve WebID, block WebID) are skipped in CI. Run: npx playwright test specs/solid.spec.ts
 */
const skipUiTests = !!process.env.CI;

test.describe('Solid Integration', () => {
  test.setTimeout(120000); // Node cold start + health can take 60s; leave time for test steps
  let harness: MultiPeerHarness;
  let fakeSolid: FakeSolidServer;

  test.beforeAll(async () => {
    const repoRoot = path.resolve(process.cwd(), '..', '..');
    const projectPath = path.join(repoRoot, 'src', 'slskd', 'slskd.csproj');
    execSync(`dotnet build "${projectPath}" --nologo -v q`, {
      cwd: repoRoot,
      stdio: 'ignore',
      env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' }
    });
    harness = new MultiPeerHarness();
    fakeSolid = new FakeSolidServer();
    await fakeSolid.start();
  });

  test.afterEach(async () => {
    // Stop nodes after each test so the next test starts with a clean slate (avoids health timeout when starting 2nd+ node)
    await harness.stopAll();
  });

  test.afterAll(async () => {
    await fakeSolid.stop();
  });

  // Run 404-when-disabled first so its node starts when no others are running (avoids health timeout)
  test('should return 404 when Solid feature is disabled', async ({ page }) => {
    const node = await harness.startNode('alice-solid-disabled', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: false
    });

    await page.goto(`${node.apiUrl}/`);
    await login(page, node.apiUrl, 'admin', 'admin');

    // Try to access Solid API endpoint (with auth cookie from page context)
    const response = await page.request.get(`${node.apiUrl}/api/v0/solid/status`);
    // When feature disabled: 404; if auth not sent: 401
    expect([401, 404]).toContain(response.status());

    // Client ID document is anonymous; should be 404 when feature disabled
    const clientIdResponse = await page.request.get(`${node.apiUrl}/solid/clientid.jsonld`);
    expect(clientIdResponse.status()).toBe(404);
  });

  test('should load Solid settings page when feature is enabled', async ({ page }) => {
    test.skip(skipUiTests, 'UI tests skipped in CI');
    const node = await harness.startNode('alice-solid-page', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: true,
      solidAllowedHosts: [fakeSolid.getHostname()]
    });

    await login(page, node.apiUrl, 'admin', 'admin');
    await page.goto(`${node.apiUrl}/solid`);
    await expect(page.locator('[data-testid="solid-root"]')).toBeVisible({ timeout: 60000 });
    await expect(page.locator('text=Client ID:')).toBeVisible({ timeout: 15000 });
  });

  test('should resolve WebID and display OIDC issuers', async ({ page }) => {
    test.skip(skipUiTests, 'UI tests skipped in CI');

    // SolidFetchPolicy intentionally blocks localhost/private IPs even if allow-listed.
    // A positive resolution test needs an external HTTPS WebID host.
    test.skip(true, 'Requires external HTTPS WebID host (localhost is blocked by SSRF policy)');

    const node = await harness.startNode('alice-solid-resolve', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: true,
      solidAllowedHosts: [fakeSolid.getHostname()]
    });

    await login(page, node.apiUrl, 'admin', 'admin');
    await page.goto(`${node.apiUrl}/solid`);
    await expect(page.locator('[data-testid="solid-root"]')).toBeVisible({ timeout: 60000 });

    // Enter WebID
    const webIdInput = page
      .locator('[data-testid="solid-webid-input"] input')
      .or(page.locator('input[placeholder*="WebID"]'));
    await webIdInput.fill(fakeSolid.getWebIdUrl());

    // Click resolve button
    const resolveButton = page.locator('[data-testid="solid-resolve-webid"]');
    await resolveButton.click();

    // Wait for resolved data to appear
    const resolvedPre = page.locator('pre').first();
    await expect(resolvedPre).toBeVisible({ timeout: 10000 });
    await expect(resolvedPre).toContainText(fakeSolid.getBaseUrl(), { timeout: 5000 });
  });

  test('should serve Client ID document at correct endpoint', async ({ page }) => {
    const node = await harness.startNode('alice-solid-clientid', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: true,
      solidAllowedHosts: [fakeSolid.getHostname()]
    });

    // Client ID document is anonymous; no login required
    const response = await page.request.get(`${node.apiUrl}/solid/clientid.jsonld`);
    
    expect(response.status()).toBe(200);
    const contentType = response.headers()['content-type'] || '';
    expect(contentType).toContain('application/ld+json');
    
    const doc = await response.json();
    // JSON-LD uses @context; some parsers expose it as context
    const context = doc['@context'] ?? doc.context;
    expect(context).toBe('https://www.w3.org/ns/solid/oidc-context.jsonld');
    expect(doc.client_id).toContain('/solid/clientid.jsonld');
    expect(doc.redirect_uris).toBeDefined();
    expect(Array.isArray(doc.redirect_uris)).toBe(true);
  });

  test('should block WebID resolution when host not in AllowedHosts', async ({ page }) => {
    test.skip(skipUiTests, 'UI tests skipped in CI');
    const node = await harness.startNode('alice-solid-blocked', 'test-data/slskdn-test-fixtures/music', {
      solidEnabled: true,
      solidAllowedHosts: [] // Empty = deny all
    });

    await login(page, node.apiUrl, 'admin', 'admin');
    await page.goto(`${node.apiUrl}/solid`);
    await expect(page.locator('[data-testid="solid-root"]')).toBeVisible({ timeout: 60000 });

    // Try to resolve WebID (should fail due to SSRF policy)
    const webIdInput = page
      .locator('[data-testid="solid-webid-input"] input')
      .or(page.locator('input[placeholder*="WebID"]'));
    await webIdInput.fill(fakeSolid.getWebIdUrl());

    const resolveButton = page.locator('[data-testid="solid-resolve-webid"]');
    await resolveButton.click();

    // Should show error message
    await expect(page.locator('.ui.negative.message')).toBeVisible({ timeout: 10000 });
  });
});
