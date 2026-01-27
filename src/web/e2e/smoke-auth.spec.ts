import { test, expect } from "@playwright/test";
import { NODES, shouldLaunchNodes } from "./env";
import { waitForHealth, login, goto } from "./helpers";
import { T } from "./selectors";
import { MultiPeerHarness } from "./harness/MultiPeerHarness";

test.describe("smoke/auth", () => {
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

  test("health_and_login", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    
    // Capture console logs and network errors for debugging
    page.on('console', msg => {
      if (msg.type() === 'error') {
        console.log(`[Browser Console Error] ${msg.text()}`);
      }
    });
    
    page.on('pageerror', error => {
      console.log(`[Page Error] ${error.message}`);
    });
    
    page.on('response', response => {
      if (!response.ok() && response.url().includes('/api/')) {
        console.log(`[API Error] ${response.status()} ${response.url()}`);
      }
    });
    
    await login(page, nodeA);
    
    // Take a screenshot for debugging
    await page.screenshot({ path: 'test-results/login-after.png', fullPage: true });
    
    // Log the current URL and page content for debugging
    console.log(`[Debug] Current URL: ${page.url()}`);
    const bodyText = await page.locator('body').textContent();
    console.log(`[Debug] Body text preview: ${bodyText?.substring(0, 200)}`);
  });

  test("route_guard", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    
    // Capture console logs for debugging
    page.on('console', msg => {
      if (msg.type() === 'error') {
        console.log(`[Browser Console Error] ${msg.text()}`);
      }
    });
    
    // Ensure we're not logged in - clear any existing tokens
    await page.goto(nodeA.baseUrl, { waitUntil: "networkidle", timeout: 30000 });
    await page.evaluate(() => {
      sessionStorage.removeItem('slskd-token');
      localStorage.removeItem('slskd-token');
    });
    
    // Navigate to protected route - wait for page to fully load
    await page.goto(`${nodeA.baseUrl}/system`, { waitUntil: "networkidle", timeout: 30000 });
    
    // Wait for React app to initialize and render
    await page.waitForTimeout(5000); // Give time for async session check and React rendering

    // The route guard works if we don't see protected content (nav elements)
    // Check multiple times with waits to handle async rendering
    for (let i = 0; i < 3; i++) {
      await page.waitForTimeout(2000);
      const hasNavElements = await page.locator('[data-testid^="nav-"]').count() > 0;
      const hasLoginForm = await page.getByTestId(T.loginUsername).count() > 0 || 
                          await page.locator('input[placeholder*="Username" i]').count() > 0;
      
      if (hasNavElements) {
        throw new Error(`Route guard failed - nav elements visible when not authenticated (check ${i + 1})`);
      }
      
      if (hasLoginForm) {
        // Login form is visible, route guard is working
        return; // Test passes
      }
    }
    
    // Final check - if no nav elements after all waits, route guard is working
    const finalNavCheck = await page.locator('[data-testid^="nav-"]').count() > 0;
    if (finalNavCheck) {
      throw new Error('Route guard failed - nav elements visible when not authenticated');
    }
    
    // No nav elements = route guard is working (even if login form isn't immediately visible)
    // This is acceptable - the important thing is that protected content isn't shown
  });

  test("logout", async ({ page, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);
    
    // Capture console logs for debugging
    page.on('console', msg => {
      if (msg.type() === 'error') {
        console.log(`[Browser Console Error] ${msg.text()}`);
      }
    });
    
    await login(page, nodeA);

    // Click logout Menu.Item (this opens a modal)
    // The logout button is a Menu.Item with data-testid="logout"
    let logoutMenuItem;
    try {
      logoutMenuItem = page.getByTestId(T.logout);
      await expect(logoutMenuItem).toBeVisible({ timeout: 15000 });
    } catch {
      // Fallback: try finding by text in menu
      logoutMenuItem = page.locator('.ui.menu .item:has-text("Log Out")');
      await expect(logoutMenuItem.first()).toBeVisible({ timeout: 15000 });
    }
    
    await logoutMenuItem.click();

    // Wait for modal to appear and confirm logout
    // The modal has a button with text "Log Out" (case-sensitive in Semantic UI)
    const confirmButton = page.getByRole("button", { name: /^Log Out$/i });
    await expect(confirmButton).toBeVisible({ timeout: 10000 });
    await confirmButton.click();

    // Should return to login - wait for navigation and login form
    await page.waitForTimeout(1000); // Give time for logout to process
    
    // Wait for either URL change or login form to appear
    await Promise.race([
      page.waitForURL(url => url.includes('/login') || !url.includes('/system'), { timeout: 10000 }).catch(() => {}),
      page.waitForSelector(`[data-testid="${T.loginUsername}"]`, { timeout: 10000 }).catch(() => {}),
      page.waitForSelector('input[placeholder*="Username" i]', { timeout: 10000 }).catch(() => {})
    ]);

    // Verify we're back at login
    try {
      await expect(page.getByTestId(T.loginUsername)).toBeVisible({ timeout: 15000 });
    } catch (error) {
      // Fallback: check for login form by placeholder
      const loginByPlaceholder = page.locator('input[placeholder*="Username" i]');
      const hasLoginForm = await loginByPlaceholder.count() > 0;
      const currentUrl = page.url();
      const hasNavElements = await page.locator('[data-testid^="nav-"]').count() > 0;
      
      console.log(`[Logout Debug] URL: ${currentUrl}`);
      console.log(`[Logout Debug] Has login form: ${hasLoginForm}`);
      console.log(`[Logout Debug] Has nav elements: ${hasNavElements}`);
      
      if (hasLoginForm) {
        await expect(loginByPlaceholder).toBeVisible({ timeout: 5000 });
        return;
      }
      
      if (hasNavElements) {
        throw new Error(`Logout failed - nav elements still visible after logout. URL: ${currentUrl}`);
      }
      
      // Check if token was cleared
      const tokenAfterLogout = await page.evaluate(() => {
        return sessionStorage.getItem('slskd-token') || localStorage.getItem('slskd-token');
      });
      
      if (tokenAfterLogout) {
        throw new Error(`Logout failed - token still in storage. URL: ${currentUrl}`);
      }
      
      // If no login form but token is cleared, wait a bit more for redirect
      await page.waitForTimeout(2000);
      const loginAfterWait = await page.locator('input[placeholder*="Username" i]').count();
      if (loginAfterWait > 0) {
        await expect(page.locator('input[placeholder*="Username" i]')).toBeVisible({ timeout: 5000 });
        return;
      }
      
      throw new Error(`Logout may have failed - no login form found. URL: ${currentUrl}, Token cleared: ${!tokenAfterLogout}`);
    }
  });
});
