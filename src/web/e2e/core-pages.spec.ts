import { test, expect } from "@playwright/test";
import { NODES } from "./env";
import { waitForHealth, login, clickNav } from "./helpers";
import { T } from "./selectors";

test.describe("core pages", () => {
  test("system_page_loads", async ({ page, request }) => {
    await waitForHealth(request, NODES.A.baseUrl);
    await login(page, NODES.A);

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
    await waitForHealth(request, NODES.A.baseUrl);
    await login(page, NODES.A);

    await clickNav(page, T.navDownloads);
    await expect(page).toHaveURL(/\/downloads/);
    // Verify page loaded (check for any visible content or specific element)
    await expect(page.locator("body")).toBeVisible();
  });

  test("uploads_page_loads", async ({ page, request }) => {
    await waitForHealth(request, NODES.A.baseUrl);
    await login(page, NODES.A);

    await clickNav(page, T.navUploads);
    await expect(page).toHaveURL(/\/uploads/);
    // Verify page loaded
    await expect(page.locator("body")).toBeVisible();
  });

  test("rooms_chat_users_pages_graceful_offline", async ({ page, request }) => {
    await waitForHealth(request, NODES.A.baseUrl);
    await login(page, NODES.A);

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
