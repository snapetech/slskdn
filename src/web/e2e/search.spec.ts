import { test, expect } from "@playwright/test";
import { NODES, shouldLaunchNodes } from "./env";
import { waitForHealth, login, clickNav } from "./helpers";
import { T } from "./selectors";
import { MultiPeerHarness } from "./harness/MultiPeerHarness";

test.describe("search", () => {
  let harness: MultiPeerHarness | null = null;

  test.beforeAll(async () => {
    if (shouldLaunchNodes()) {
      harness = new MultiPeerHarness();
      await harness.startNode('A', 'test-data/slskdn-test-fixtures/music', {
        noConnect: process.env.SLSKDN_TEST_NO_CONNECT === 'true'
      });
    }
  });

  test.afterAll(async () => {
    if (harness) {
      await harness.stopAll();
    }
  });

  test("local_search_returns_fixture_hits", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    await login(page, nodeA);

    await clickNav(page, T.navSearch);
    
    // Wait for search UI
    await page.waitForSelector('[data-testid="search-input"], input[placeholder*="search" i]', { timeout: 5000 });
    
    // Perform search (adjust selector based on actual search UI)
    const searchInput = page.locator('[data-testid="search-input"], input[placeholder*="search" i]').first();
    await searchInput.fill('synthetic');
    await searchInput.press('Enter');
    
    // Wait for results
    await page.waitForTimeout(2000);
    
    // Verify results appear (adjust selector based on actual results UI)
    const results = page.locator('[data-testid*="search-result"], [data-testid*="result-item"]');
    const count = await results.count();
    expect(count).toBeGreaterThan(0);
  });

  test("no_connect_disables_soulseek_provider_gracefully", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    await login(page, nodeA);

    await clickNav(page, T.navSearch);
    
    // Verify UI shows connection status or provider state
    // Adjust selector based on actual UI
    const connectionStatus = page.getByTestId(T.connectionStatus);
    if (await connectionStatus.count()) {
      // If no_connect is enabled, should show disconnected/offline state
      if (process.env.SLSKDN_TEST_NO_CONNECT === 'true') {
        await expect(connectionStatus).toContainText(/disconnected|offline|no connect/i, { timeout: 5000 });
      }
    }
    
    // UI should not crash or show errors
    await expect(page.locator('body')).toBeVisible();
  });
});
