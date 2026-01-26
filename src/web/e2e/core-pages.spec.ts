import { test, expect } from "@playwright/test";
import { NODES, shouldLaunchNodes } from "./env";
import { waitForHealth, login, clickNav } from "./helpers";
import { T } from "./selectors";
import { MultiPeerHarness } from "./harness/MultiPeerHarness";

test.describe("core pages", () => {
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

  test("system_page_loads", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    await login(page, nodeA);

    await clickNav(page, T.navSystem);
    await expect(page).toHaveURL(/\/system/);

    // If you have a shares tab
    const sharesTab = page.getByTestId(T.systemTabShares);
    if (await sharesTab.count()) {
      await sharesTab.click();
      await expect(page.getByTestId(T.systemSharesTable)).toBeVisible();
    }
  });

  test("downloads_page_loads", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    await login(page, nodeA);

    await clickNav(page, T.navDownloads);
    await expect(page).toHaveURL(/\/downloads/);
    await expect(page.getByTestId(T.downloadsRoot)).toBeVisible();
  });

  test("uploads_page_loads", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    await login(page, nodeA);

    await clickNav(page, T.navUploads);
    await expect(page).toHaveURL(/\/uploads/);
    await expect(page.getByTestId(T.uploadsRoot)).toBeVisible();
  });

  test("rooms_chat_users_pages_graceful_offline", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    await login(page, nodeA);

    for (const nav of [T.navRooms, T.navChat, T.navUsers]) {
      await clickNav(page, nav);
      await expect(page.locator("body")).toBeVisible();

      // Add a universal "connection status" indicator testid if you don't have one
      const status = page.getByTestId(T.connectionStatus);
      if (await status.count()) {
        await expect(status).toBeVisible();
      }
    }
  });
});
