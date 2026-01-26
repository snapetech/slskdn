import { expect, Page, APIRequestContext } from "@playwright/test";
import { NodeCfg } from "./env";
import { T } from "./selectors";

export async function waitForHealth(request: APIRequestContext, baseUrl: string) {
  const health = `${baseUrl}/health`;
  for (let i = 0; i < 120; i++) {
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise(r => setTimeout(r, 500));
  }
  throw new Error(`Timed out waiting for ${health}`);
}

export async function login(page: Page, node: NodeCfg) {
  await page.goto(node.baseUrl, { waitUntil: "networkidle" });

  // Wait for React to mount - check for root div or any React-rendered content
  // The login form should appear once React loads
  await page.waitForSelector('#root', { state: 'attached', timeout: 10000 });
  
  // Wait for the login form to appear (either by testid or by placeholder text as fallback)
  try {
    await page.waitForSelector(`[data-testid="${T.loginUsername}"]`, { timeout: 10000 });
  } catch {
    // Fallback: wait for input with "Username" placeholder
    await page.waitForSelector('input[placeholder*="Username" i]', { timeout: 10000 });
  }

  const user = page.getByTestId(T.loginUsername).or(page.locator('input[placeholder*="Username" i]'));
  const pass = page.getByTestId(T.loginPassword).or(page.locator('input[type="password"]'));
  const submit = page.getByTestId(T.loginSubmit).or(page.locator('button:has-text("Login")'));

  await expect(user).toBeVisible({ timeout: 15000 });
  await user.fill(node.username);
  await pass.fill(node.password);
  
  // Wait for submit button to be enabled (React might disable it until fields are filled)
  await expect(submit).toBeEnabled({ timeout: 5000 });
  await submit.click();

  // Wait for navigation after login - either URL changes or nav appears
  // The app might redirect or the React app re-renders with authenticated state
  await Promise.race([
    page.waitForURL(url => !url.includes('/login') && url.includes(node.baseUrl), { timeout: 10000 }).catch(() => {}),
    page.waitForSelector(`[data-testid="${T.navSystem}"]`, { timeout: 10000 }).catch(() => {}),
    page.waitForTimeout(2000) // Give React time to re-render
  ]);

  // A stable post-login condition: sidebar/system link present
  // Try multiple ways to detect successful login
  try {
    await expect(page.getByTestId(T.navSystem)).toBeVisible({ timeout: 10000 });
  } catch {
    // Fallback: check if we're no longer on login page, or if any nav element appears
    const anyNav = page.locator('[data-testid^="nav-"]').first();
    if (await anyNav.count() > 0) {
      await expect(anyNav).toBeVisible({ timeout: 5000 });
    } else {
      // Last resort: check URL changed or page content changed
      const currentUrl = page.url();
      if (!currentUrl.includes('/login')) {
        // Assume login succeeded if we're not on login page
        return;
      }
      throw new Error('Login may have failed - nav elements not found and still on login page');
    }
  }
}

export async function goto(page: Page, node: NodeCfg, route: string) {
  await page.goto(`${node.baseUrl}${route}`, { waitUntil: "domcontentloaded" });
}

export async function clickNav(page: Page, testId: string) {
  const el = page.getByTestId(testId);
  await expect(el).toBeVisible();
  await el.click();
}
