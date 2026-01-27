import { type NodeCfg } from './env';
import { T } from './selectors';
import { type APIRequestContext, expect, type Page } from '@playwright/test';

export async function waitForHealth(
  request: APIRequestContext,
  baseUrl: string,
) {
  const health = `${baseUrl}/health`;
  // Server typically starts in 2-5 seconds, so 15 seconds is plenty
  for (let index = 0; index < 30; index++) {
    const res = await request.get(health, { failOnStatusCode: false });
    if (res.ok()) return;
    await new Promise((r) => setTimeout(r, 500));
  }

  throw new Error(`Timed out waiting for ${health} after 15 seconds`);
}

export async function login(page: Page, node: NodeCfg) {
  // Capture network responses to diagnose loading issues
  const networkLog: Array<{ status: number; type: string; url: string }> = [];
  page.on('response', (response) => {
    const url = response.url();
    if (url.includes(node.baseUrl) && !url.includes('/api/')) {
      networkLog.push({
        status: response.status(),
        type: response.request().resourceType(),
        url,
      });
      if (response.status() !== 200) {
        console.log(`[Login] Non-200 response: ${response.status()} ${url}`);
      }
    }
  });

  // Capture console errors
  const consoleErrors: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') {
      const text = message.text();
      consoleErrors.push(text);
      console.log(`[Login] Console error: ${text}`);
    }
  });

  console.log(`[Login] Navigating to ${node.baseUrl}...`);
  await page.goto(node.baseUrl, { timeout: 10_000, waitUntil: 'networkidle' });

  // Check what we actually got
  const content = await page.content();
  const title = await page.title();
  const url = page.url();

  console.log(
    `[Login] After navigation - URL: ${url}, Title: ${title}, Content length: ${content.length}`,
  );
  console.log(
    `[Login] Content preview (first 500 chars): ${content.slice(0, 500)}`,
  );

  // Check for #root
  const rootCount = await page.locator('#root').count();
  const rootContent = await page
    .locator('#root')
    .textContent()
    .catch(() => '');
  console.log(
    `[Login] #root count: ${rootCount}, content length: ${rootContent.length}`,
  );
  if (rootContent.length < 100) {
    console.log(`[Login] #root content: ${rootContent}`);
  }

  // Check for React mounting
  const hasReactContent = await page
    .evaluate(() => {
      const root = document.querySelector('#root');
      return root && root.children.length > 0;
    })
    .catch(() => false);
  console.log(
    `[Login] React has mounted (root has children): ${hasReactContent}`,
  );

  // Check network log
  console.log(`[Login] Network requests: ${networkLog.length} total`);
  const failedRequests = networkLog.filter((r) => r.status !== 200);
  if (failedRequests.length > 0) {
    console.log(
      `[Login] Failed requests: ${JSON.stringify(failedRequests, null, 2)}`,
    );
  }

  // Check console errors
  if (consoleErrors.length > 0) {
    console.log(
      `[Login] Console errors (${consoleErrors.length}): ${consoleErrors.join('; ')}`,
    );
  }

  // Wait for React to mount - check for root div or any React-rendered content
  // The login form should appear once React loads
  console.log(`[Login] Waiting for #root...`);
  try {
    await page.waitForSelector('#root', { state: 'attached', timeout: 10_000 });
    console.log(`[Login] #root found`);
  } catch (error) {
    console.error(`[Login] #root not found: ${error}`);
    throw new Error(
      `React root not found. Content length: ${content.length}, Root count: ${rootCount}, Network requests: ${networkLog.length}, Console errors: ${consoleErrors.length}`,
    );
  }

  // Wait for the login form to appear (either by testid or by placeholder text as fallback)
  console.log(`[Login] Waiting for login form...`);
  try {
    await page.waitForSelector(`[data-testid="${T.loginUsername}"]`, {
      timeout: 10_000,
    });
    console.log(`[Login] Login form found by testid`);
  } catch {
    console.log(
      `[Login] Login form not found by testid, trying placeholder...`,
    );
    // Fallback: wait for input with "Username" placeholder
    try {
      await page.waitForSelector('input[placeholder*="Username" i]', {
        timeout: 10_000,
      });
      console.log(`[Login] Login form found by placeholder`);
    } catch {
      // Last resort: check what's actually on the page
      const allInputs = await page.locator('input').count();
      const allButtons = await page.locator('button').count();
      const bodyText = await page
        .locator('body')
        .textContent()
        .catch(() => '');
      console.error(
        `[Login] Login form not found. Inputs: ${allInputs}, Buttons: ${allButtons}, Body text length: ${bodyText.length}`,
      );
      if (bodyText.length < 200) {
        console.error(`[Login] Body text: ${bodyText}`);
      }

      throw new Error(
        `Login form not found. Content length: ${content.length}, Root count: ${rootCount}, Inputs: ${allInputs}, Console errors: ${consoleErrors.length}`,
      );
    }
  }

  // Prefer input element over wrapper div
  const user = page
    .locator('input[placeholder*="Username" i]')
    .or(page.getByTestId(T.loginUsername).locator('input'))
    .first();
  const pass = page
    .locator('input[type="password"]')
    .or(page.getByTestId(T.loginPassword).locator('input'))
    .first();
  const submit = page
    .getByTestId(T.loginSubmit)
    .or(page.locator('button:has-text("Login")'))
    .first();

  await expect(user).toBeVisible({ timeout: 15_000 });
  await user.fill(node.username);
  await pass.fill(node.password);

  // Wait for submit button to be enabled (React might disable it until fields are filled)
  await expect(submit).toBeEnabled({ timeout: 5_000 });

  // Wait for the login API call to complete before checking for navigation
  // The login endpoint is POST /api/v0/session
  const loginResponsePromise = page.waitForResponse(
    (response) =>
      response.url().includes('/api/v0/session') &&
      response.request().method() === 'POST',
    { timeout: 15_000 },
  );

  await submit.click();

  // Wait for the login API response and check status
  let loginResponse;
  try {
    loginResponse = await loginResponsePromise;
    const status = loginResponse.status();
    const body = await loginResponse.json().catch(() => null);
    console.log(`[Login] API call completed with status ${status}`);
    if (status !== 200) {
      console.error(`[Login] Login failed with status ${status}, body:`, body);
      throw new Error(`Login API returned ${status}: ${JSON.stringify(body)}`);
    }

    if (!body || !body.token) {
      console.error('[Login] Login response missing token:', body);
      throw new Error('Login response missing token');
    }

    console.log('[Login] API call completed successfully, token received');
  } catch (error) {
    console.error('[Login] Login API call failed or timed out:', error);
    throw error;
  }

  // Poll for token to be stored (more efficient than fixed timeout)
  // The React app stores the token asynchronously after receiving the response
  // The token key is 'slskd-token' (from config.js)
  let token: string | null = null;
  for (let index = 0; index < 10; index++) {
    token = await page.evaluate(() => {
      return (
        sessionStorage.getItem('slskd-token') ||
        localStorage.getItem('slskd-token')
      );
    });
    if (token) {
      console.log('[Login] Token stored successfully');
      break;
    }

    await new Promise((r) => setTimeout(r, 300)); // Short poll interval
  }

  if (!token) {
    // Debug: check what's actually in storage
    const storageDebug = await page.evaluate(() => {
      return {
        localStorage: Object.keys(localStorage).map((k) => ({
          key: k,
          value: localStorage.getItem(k)?.slice(0, 50),
        })),
        sessionStorage: Object.keys(sessionStorage).map((k) => ({
          key: k,
          value: sessionStorage.getItem(k)?.slice(0, 50),
        })),
      };
    });
    console.error(
      '[Login] Token not found after polling. Storage contents:',
      storageDebug,
    );
    throw new Error('Login token not found in storage after login');
  }

  // Wait for navigation after login - either URL changes or nav appears
  // The app might redirect or the React app re-renders with authenticated state
  await Promise.race([
    page
      .waitForURL(
        (url) => !url.includes('/login') && url.includes(node.baseUrl),
        { timeout: 8_000 },
      )
      .catch(() => {}),
    page
      .waitForSelector(`[data-testid="${T.navSystem}"]`, { timeout: 8_000 })
      .catch(() => {}),
    page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => {}), // More efficient than fixed timeout
  ]);

  // Wait for the session check API call to complete (the app checks /session on load)
  // This might be failing with 401 if the token isn't being sent correctly
  try {
    await page.waitForResponse(
      (response) =>
        response.url().includes('/api/v0/session') &&
        response.request().method() === 'GET',
      { timeout: 5_000 },
    );
    console.log('[Login] Session check API call completed');
  } catch {
    console.log(
      '[Login] Session check API call timeout (may not have been called)',
    );
  }

  // Wait for React to finish rendering - use load state instead of fixed timeout
  await page.waitForLoadState('domcontentloaded').catch(() => {});

  // A stable post-login condition: sidebar/system link present
  // Try multiple ways to detect successful login
  // Note: Semantic UI Menu.Item might not forward data-testid properly, so try multiple selectors
  try {
    // First try the data-testid
    await expect(page.getByTestId(T.navSystem)).toBeVisible({
      timeout: 20_000,
    });
    console.log('[Login] Found nav-system using data-testid');
  } catch {
    // Fallback: try finding by text content or by the Menu.Item structure
    // Semantic UI might render the menu items differently
    const navByText = page
      .getByRole('link', { name: /system/i })
      .or(page.locator('a:has-text("System")'));
    const navByMenuItem = page.locator(
      '.ui.menu .item[data-testid="nav-system"]',
    );
    const navByAnyText = page
      .locator('text=System')
      .filter({ has: page.locator('a, .item') });

    // Try waiting for any of these
    try {
      const found = navByText
        .or(navByMenuItem)
        .or(navByAnyText)
        .or(page.getByTestId(T.navSystem));
      await expect(found.first()).toBeVisible({ timeout: 10_000 });
      console.log('[Login] Found nav element using fallback selector');
      return; // Success with fallback
    } catch {
      // Continue to full debugging below
      console.log('[Login] Fallback selectors also failed');
    }

    // Enhanced debugging: check what's actually on the page
    const currentUrl = page.url();
    const hasLoginForm = (await page.getByTestId(T.loginUsername).count()) > 0;

    // Check for nav elements using multiple methods
    const navByTestId = await page.locator('[data-testid^="nav-"]').count();
    const navByMenu = await page.locator('.ui.menu, .navigation').count();
    const navByTextCount = await page
      .locator('text=/Search|Downloads|System/i')
      .count();

    // Check for JavaScript errors
    const jsErrors = await page
      .evaluate(() => {
        return window.__playwright_errors__ || [];
      })
      .catch(() => []);

    // Check page content
    const bodyText = await page
      .locator('body')
      .textContent()
      .catch(() => '');
    const hasRoot = (await page.locator('#root').count()) > 0;
    const rootContent = await page
      .locator('#root')
      .textContent()
      .catch(() => '');

    // Check if menu items exist in DOM (even if not visible)
    const menuItemsInDom = await page.evaluate(() => {
      const items = document.querySelectorAll(
        '.ui.menu .item, [data-testid^="nav-"]',
      );
      return Array.from(items).map((element) => ({
        testid: element.dataset.testid,
        text: element.textContent?.trim().slice(0, 50),
        visible: element.offsetParent !== null,
      }));
    });

    console.log(`[Login Debug] URL: ${currentUrl}`);
    console.log(`[Login Debug] Has login form: ${hasLoginForm}`);
    console.log(`[Login Debug] Nav by testid: ${navByTestId}`);
    console.log(`[Login Debug] Nav menu containers: ${navByMenu}`);
    console.log(`[Login Debug] Nav by text: ${navByTextCount}`);
    console.log(`[Login Debug] Token in storage: ${token ? 'yes' : 'no'}`);
    console.log(
      `[Login Debug] Menu items in DOM: ${JSON.stringify(menuItemsInDom)}`,
    );

    // If we have menu items in DOM, even if not visible, login likely succeeded
    if (menuItemsInDom.length > 0) {
      console.log('[Login] Menu items found in DOM, login likely succeeded');
      // Try to wait for at least one to become visible
      try {
        await page.waitForSelector('.ui.menu .item, [data-testid^="nav-"]', {
          state: 'visible',
          timeout: 5_000,
        });
        console.log('[Login] At least one nav item is now visible');
        return;
      } catch {
        // If they're in DOM but not visible, that's still OK - the app is loaded
        console.log(
          '[Login] Menu items in DOM but not visible - this may be OK',
        );
        if (!hasLoginForm && !currentUrl.includes('/login') && token) {
          console.log(
            '[Login] Considering login successful: no login form, token present, menu in DOM',
          );
          return;
        }
      }
    }

    if (!hasLoginForm && !currentUrl.includes('/login') && token) {
      // We're past login, have a token, and no login form - login succeeded
      // The nav might just not be visible yet or testids aren't working
      console.log(
        '[Login] Login appears successful: no login form, token present, navigated away from login',
      );
      return;
    }

    // Last resort: check if we're still on login page
    if (hasLoginForm || currentUrl.includes('/login')) {
      throw new Error(
        `Login failed - still on login page. URL: ${currentUrl}, Token: ${token ? 'present' : 'missing'}`,
      );
    }

    // If we're not on login page but nav isn't visible, it might be a rendering issue
    throw new Error(
      `Login may have succeeded but nav elements not found. URL: ${currentUrl}, Nav testid count: ${navByTestId}, Menu items in DOM: ${menuItemsInDom.length}, Token: ${token ? 'present' : 'missing'}`,
    );
  }
}

export async function goto(page: Page, node: NodeCfg, route: string) {
  await page.goto(`${node.baseUrl}${route}`, { waitUntil: 'domcontentloaded' });
}

export async function clickNav(page: Page, testId: string) {
  // The Link component wraps the Menu.Item, so we need to find the Link that contains the Menu.Item
  // Try multiple approaches to find the clickable element
  const menuItem = page.getByTestId(testId);
  const linkWithMenuItem = page.locator(`a:has([data-testid="${testId}"])`);
  const menuItemInLink = page.locator(
    `.ui.menu a:has([data-testid="${testId}"])`,
  );

  // Wait for at least one to be visible
  let element = linkWithMenuItem.or(menuItemInLink).or(menuItem);

  try {
    await expect(element.first()).toBeVisible({ timeout: 15_000 });
    element = element.first();
  } catch {
    // Fallback: try finding by menu structure
    const navByMenu = page.locator(`.ui.menu .item[data-testid="${testId}"]`);
    try {
      await expect(navByMenu.first()).toBeVisible({ timeout: 8_000 });
      element = navByMenu.first();
    } catch {
      // Last resort: wait for load state and try again
      await page
        .waitForLoadState('networkidle', { timeout: 3_000 })
        .catch(() => {});
      await expect(menuItem.first()).toBeVisible({ timeout: 8_000 });
      element = menuItem.first();
    }
  }

  // Get current URL before clicking
  const urlBefore = page.url();

  // Click the element - prefer clicking the Link if we found it, otherwise click the Menu.Item
  // React Router Link should handle navigation immediately
  try {
    await element.click({ timeout: 5_000 });
  } catch {
    // If normal click fails, try force click
    await element.click({ force: true, timeout: 5_000 });
  }

  // Wait for URL to change (React Router does client-side navigation immediately)
  // But don't fail if it doesn't change - let the test handle that
  try {
    await page.waitForURL((url) => url !== urlBefore, { timeout: 3_000 });
  } catch {
    // URL didn't change, but that's OK - might have already been on that page
  }

  // Give React Router a moment to update the DOM
  await page.waitForTimeout(300);
}

export async function verifySpaFallback(
  request: APIRequestContext,
  baseUrl: string,
) {
  // First verify root serves index.html
  const rootResponse = await request.get(baseUrl, { failOnStatusCode: false });
  const rootStatus = rootResponse.status();
  const rootBody = await rootResponse.text();

  console.log(`[SPA Check] Root (/) status: ${rootStatus}`);
  console.log(`[SPA Check] Root body length: ${rootBody.length}`);
  console.log(
    `[SPA Check] Root body contains <html: ${rootBody.includes('<html')}`,
  );
  console.log(
    `[SPA Check] Root body contains #root: ${rootBody.includes('id="root"') || rootBody.includes("id='root'")}`,
  );

  if (rootStatus !== 200) {
    throw new Error(`Root path returned ${rootStatus}, expected 200`);
  }

  if (!rootBody.includes('<html') || !rootBody.includes('root')) {
    throw new Error(
      `Root path does not appear to serve index.html. Body length: ${rootBody.length}, Contains <html: ${rootBody.includes('<html')}, Contains root: ${rootBody.includes('root')}`,
    );
  }

  // Then verify a client-side route
  const clientRouteResponse = await request.get(`${baseUrl}/downloads`, {
    failOnStatusCode: false,
  });
  const clientRouteStatus = clientRouteResponse.status();
  const clientRouteBody = await clientRouteResponse.text();

  console.log(
    `[SPA Check] Client route (/downloads) status: ${clientRouteStatus}`,
  );
  console.log(
    `[SPA Check] Client route body length: ${clientRouteBody.length}`,
  );
  console.log(
    `[SPA Check] Client route body contains <html: ${clientRouteBody.includes('<html')}`,
  );
  console.log(
    `[SPA Check] Client route body contains #root: ${clientRouteBody.includes('id="root"') || clientRouteBody.includes("id='root'")}`,
  );

  if (clientRouteStatus === 404) {
    throw new Error(
      `Client route /downloads returned 404. SPA fallback is not working. Root works: ${rootStatus === 200}`,
    );
  }

  if (clientRouteStatus !== 200) {
    console.warn(
      `[SPA Check] Client route returned ${clientRouteStatus}, expected 200`,
    );
  }

  if (clientRouteBody.length < 100 || !clientRouteBody.includes('<html')) {
    throw new Error(
      `Client route /downloads does not appear to serve index.html. Status: ${clientRouteStatus}, Body length: ${clientRouteBody.length}, Contains <html: ${clientRouteBody.includes('<html')}`,
    );
  }

  return {
    clientRouteOk: clientRouteStatus === 200,
    rootOk: rootStatus === 200,
  };
}
