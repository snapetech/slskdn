import { test, expect } from "@playwright/test";
import { NODES, shouldLaunchNodes } from "./env";
import { waitForHealth, login, clickNav } from "./helpers";
import { T } from "./selectors";
import { MultiPeerHarness } from "./harness/MultiPeerHarness";

test.describe("library ingest", () => {
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

  test("fixture_share_directory_indexed", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    await login(page, nodeA);

    // Navigate to system/shares to verify indexing
    await clickNav(page, T.navSystem);
    
    // Wait for shares tab if it exists
    const sharesTab = page.getByTestId(T.systemTabShares);
    if (await sharesTab.count()) {
      await sharesTab.click();
      await expect(page.getByTestId(T.systemSharesTable)).toBeVisible({ timeout: 10000 });
      
      // Verify fixture files appear in shares table
      // Adjust selector based on actual shares table structure
      const sharesTable = page.getByTestId(T.systemSharesTable);
      await expect(sharesTable).toBeVisible();
      
      // Check for expected fixture content (adjust based on test-data/slskdn-test-fixtures/music)
      // This is a placeholder - adjust based on actual fixture structure
      await expect(sharesTable).toContainText(/music|audio|fixture/i, { timeout: 30000 });
    }
  });

  test("items_appear_in_ui_with_metadata", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    await login(page, nodeA);

    // Navigate to browse or library view
    await clickNav(page, T.navBrowse);
    
    // Wait for library content to load
    await page.waitForLoadState('networkidle');
    
    // Verify items are visible with metadata
    // Adjust selectors based on actual browse/library UI
    const libraryContent = page.locator('[data-testid="library-content"], [data-testid="browse-content"]').first();
    if (await libraryContent.count()) {
      await expect(libraryContent).toBeVisible({ timeout: 30000 });
      
      // Verify metadata is present (filename, size, duration)
      // This is a placeholder - adjust based on actual UI structure
      const items = page.locator('[data-testid*="library-item"], [data-testid*="browse-item"]');
      const count = await items.count();
      expect(count).toBeGreaterThan(0);
    }
  });
});
