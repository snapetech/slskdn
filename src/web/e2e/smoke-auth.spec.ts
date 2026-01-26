import { test, expect } from "@playwright/test";
import { NODES } from "./env";
import { waitForHealth, login, goto } from "./helpers";
import { T } from "./selectors";

test.describe("smoke/auth", () => {
  test("health_and_login", async ({ page, request }) => {
    await waitForHealth(request, NODES.A.baseUrl);
    await login(page, NODES.A);
  });

  test("route_guard", async ({ page, request }) => {
    await waitForHealth(request, NODES.A.baseUrl);
    await goto(page, NODES.A, "/system");

    // Expect login UI instead of system nav when not authenticated
    await expect(page.getByTestId(T.loginUsername)).toBeVisible();
  });

  test("logout", async ({ page, request }) => {
    await waitForHealth(request, NODES.A.baseUrl);
    await login(page, NODES.A);

    // Click logout (opens modal)
    const logout = page.getByTestId(T.logout);
    await logout.click();

    // Confirm logout in modal
    await page.getByRole("button", { name: /log out/i }).click();

    // Should return to login
    await expect(page.getByTestId(T.loginUsername)).toBeVisible();
  });
});
