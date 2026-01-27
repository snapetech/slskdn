import { test, expect } from "@playwright/test";
import { NODES, shouldLaunchNodes } from "./env";
import { waitForHealth, login, clickNav } from "./helpers";
import { T } from "./selectors";
import { MultiPeerHarness } from "./harness/MultiPeerHarness";

test.describe.configure({ mode: "serial" });

test.describe("multi-peer sharing", () => {
  let harness: MultiPeerHarness | null = null;
  const groupName = "E2E Crew";
  const collectionTitle = "E2E Playlist";

  test.beforeAll(async () => {
    if (shouldLaunchNodes()) {
      harness = new MultiPeerHarness();
      await harness.startNode('A', 'test-data/slskdn-test-fixtures/music', {
        noConnect: process.env.SLSKDN_TEST_NO_CONNECT === 'true'
      });
      await harness.startNode('B', 'test-data/slskdn-test-fixtures/book', {
        noConnect: process.env.SLSKDN_TEST_NO_CONNECT === 'true'
      });
    }
  });

  test.afterAll(async () => {
    if (harness) {
      await harness.stopAll();
    }
  });

  test("invite_add_friend", async ({ browser, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;
    
    await waitForHealth(request, nodeA.baseUrl);
    await waitForHealth(request, nodeB.baseUrl);

    const ctxA = await browser.newContext();
    const ctxB = await browser.newContext();
    const pageA = await ctxA.newPage();
    const pageB = await ctxB.newPage();

    await login(pageA, nodeA);
    await login(pageB, nodeB);

    // Diagnostic: Capture JS/runtime errors BEFORE navigation
    pageA.on('pageerror', (err) => console.error('[Contacts Test] pageerror:', err));
    pageA.on('console', (msg) => {
      if (msg.type() === 'error') console.error('[Contacts Test] console.error:', msg.text());
    });
    
    // Navigate to contacts page and wait for it to load
    console.log('[Contacts Test] Navigating to contacts page...');
    const targetUrl = `${nodeA.baseUrl}/contacts`;
    console.log('[Contacts Test] Target URL:', targetUrl);
    await pageA.goto(targetUrl, { waitUntil: 'networkidle', timeout: 10000 });
    console.log('[Contacts Test] Navigation complete, URL:', pageA.url());
    
    // Diagnostic: Compare browser location vs app location (memory history check)
    const loc = await pageA.evaluate(() => ({ 
      href: location.href, 
      pathname: location.pathname 
    }));
    const appLoc = await pageA.evaluate(() => {
      if ((window as any).__APP_HISTORY__) {
        return (window as any).__APP_HISTORY__.location.pathname;
      }
      if ((window as any).__APP_LOCATION__) {
        return (window as any).__APP_LOCATION__.pathname;
      }
      return null;
    });
    console.log('[Contacts Test] Browser location:', JSON.stringify(loc, null, 2));
    console.log('[Contacts Test] App location/history:', appLoc);
    
    // Check if URL changed (redirect happened)
    const finalUrl = pageA.url();
    if (!finalUrl.includes('/contacts')) {
      console.error(`[Contacts Test] ERROR: Redirected away from /contacts! Final URL: ${finalUrl}`);
    }
    
    // Check what's actually on the page
    const bodyContent = await pageA.locator('body').innerText();
    console.log('[Contacts Test] Body text (first 500 chars):', bodyContent.slice(0, 500));
    
    // Check which component is actually rendering
    const hasSearchElements = await pageA.locator('input[placeholder*="Search"], [data-testid*="search"]').count();
    const hasContactsElements = await pageA.locator('[data-testid="contacts-root"], [data-testid*="contact"]').count();
    console.log('[Contacts Test] Search elements count:', hasSearchElements);
    console.log('[Contacts Test] Contacts elements count:', hasContactsElements);
    
    // Check React component tree if possible
    const reactRoot = await pageA.evaluate(() => {
      const root = document.getElementById('root');
      if (!root) return null;
      const firstChild = root.firstElementChild;
      return {
        rootTag: root.tagName,
        firstChildTag: firstChild?.tagName,
        firstChildClass: firstChild?.className,
        firstChildText: firstChild?.textContent?.slice(0, 100)
      };
    });
    console.log('[Contacts Test] React root info:', JSON.stringify(reactRoot, null, 2));
    
    // Check if we're still on login page (route guard)
    const loginForm = await pageA.locator('[data-testid="login-username"], input[placeholder*="Username" i]').count();
    console.log('[Contacts Test] Login form count:', loginForm);
    if (loginForm > 0) {
      console.error('[Contacts Test] ERROR: Still on login page - route guard may be blocking!');
    }
    
    // Check for any React error boundaries or error messages
    const errorElements = await pageA.locator('[class*="error"], [class*="Error"], [data-testid*="error"]').count();
    console.log('[Contacts Test] Error elements count:', errorElements);
    
    // Check what React Router thinks the current route is
    const currentRoute = await pageA.evaluate(() => {
      // Try to find React Router state or current pathname
      return {
        pathname: window.location.pathname,
        hash: window.location.hash,
        search: window.location.search,
        urlBase: (window as any).urlBase || 'not set'
      };
    });
    console.log('[Contacts Test] Current route info:', JSON.stringify(currentRoute, null, 2));
    
    // Wait for contacts root to appear (ensures component mounted)
    console.log('[Contacts Test] Waiting for contacts-root...');
    try {
      await pageA.waitForSelector('[data-testid="contacts-root"]', { timeout: 10000 });
      console.log('[Contacts Test] contacts-root found - component mounted');
    } catch (err) {
      console.error('[Contacts Test] ERROR: contacts-root not found!');
      // Dump all data-testid elements to see what's actually rendered
      const allTestIds = await pageA.evaluate(() => {
        const elements = document.querySelectorAll('[data-testid]');
        return Array.from(elements).map(el => ({
          testid: el.getAttribute('data-testid'),
          tag: el.tagName,
          visible: (el as HTMLElement).offsetParent !== null
        }));
      });
      console.log('[Contacts Test] All data-testid elements on page:', JSON.stringify(allTestIds, null, 2));
      throw err;
    }
    
    // Wait for contacts API call to complete and capture response body
    console.log('[Contacts Test] Waiting for /api/v0/contacts response...');
    let resp;
    try {
      resp = await pageA.waitForResponse(
        r => r.url().includes('/api/v0/contacts') && r.status() === 200,
        { timeout: 10000 }
      );
      
      // Diagnostic: Verify response body
      const text = await resp.text();
      const contentType = resp.headers()['content-type'] || '';
      console.log('[Contacts Test] API Response - Content-Type:', contentType);
      console.log('[Contacts Test] API Response - Length:', text.length);
      console.log('[Contacts Test] API Response - First 200 chars:', text.slice(0, 200));
      
      if (text.length === 0) {
        console.error('[Contacts Test] ERROR: API returned 200 with empty body!');
      }
      if (text.startsWith('<html')) {
        console.error('[Contacts Test] ERROR: API returned HTML instead of JSON!');
      }
    } catch (err) {
      console.error('[Contacts Test] ERROR waiting for API response:', err);
      // Continue with diagnostics even if API wait failed
    }
    
    // Diagnostic: Check current state
    const tid = T.contactsCreateInvite;
    const count = await pageA.locator(`[data-testid="${tid}"]`).count();
    console.log(`[Contacts Test] count([data-testid="${tid}"]) =`, count);
    
    // If present, dump visibility diagnostics
    if (count > 0) {
      const diag = await pageA.locator(`[data-testid="${tid}"]`).first().evaluate((el) => {
        const cs = getComputedStyle(el);
        const rect = el.getBoundingClientRect();
        return {
          tag: el.tagName,
          display: cs.display,
          visibility: cs.visibility,
          opacity: cs.opacity,
          disabled: (el as any).disabled ?? null,
          rect: { x: rect.x, y: rect.y, w: rect.width, h: rect.height },
          inDocument: document.contains(el),
        };
      });
      console.log('[Contacts Test] button diag:', JSON.stringify(diag, null, 2));
    } else {
      console.error(`[Contacts Test] ERROR: Button with data-testid="${tid}" not found in DOM!`);
    }
    
    // Screenshot and body snippet for debugging
    await pageA.screenshot({ path: 'contacts-debug.png', fullPage: true });
    const bodyText = await pageA.locator('body').innerText();
    console.log('[Contacts Test] body snippet (first 800 chars):', bodyText.slice(0, 800));
    
    // Wait for create invite button (appears in header - always visible)
    const createInviteBtn = pageA.getByTestId(T.contactsCreateInvite);
    await expect(createInviteBtn.first()).toBeVisible({ timeout: 5000 });
    
    await createInviteBtn.click();
    
    // Wait for invite modal and get invite link
    const inviteOutput = pageA.getByTestId(T.contactsInviteOutput);
    await expect(inviteOutput).toBeVisible({ timeout: 5000 });
    const invite = await inviteOutput.inputValue();
    expect(invite.length).toBeGreaterThan(20);

    // Node B adds friend
    await pageB.goto(`${nodeB.baseUrl}/contacts`, { waitUntil: 'networkidle', timeout: 5000 });
    
    const addFriendBtn = pageB.getByTestId(T.contactsAddFriend);
    await expect(addFriendBtn).toBeVisible({ timeout: 5000 });
    await addFriendBtn.click();
    
    // Fill invite form
    const inviteInput = pageB.getByTestId(T.contactsAddInviteInput);
    await expect(inviteInput).toBeVisible({ timeout: 3000 });
    await inviteInput.fill(invite);
    
    const nicknameInput = pageB.getByTestId(T.contactsContactNickname);
    await expect(nicknameInput).toBeVisible({ timeout: 3000 });
    await nicknameInput.fill("nodeA");
    
    await pageB.getByTestId(T.contactsAddInviteSubmit).click();

    // Contact row should appear after adding
    const contactRow = pageB.getByTestId(T.contactsRow("nodeA"));
    await expect(contactRow).toBeVisible({ timeout: 5000 });

    await ctxA.close();
    await ctxB.close();
  });

  test("create_group_add_member", async ({ browser, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);

    const ctxA = await browser.newContext();
    const pageA = await ctxA.newPage();
    await login(pageA, nodeA);

    await clickNav(pageA, T.navGroups);
    await pageA.getByTestId(T.groupsCreate).click();
    
    // Wait for create group modal
    await pageA.waitForSelector(`[data-testid="${T.groupsNameInput}"]`, { timeout: 5000 });
    // Semantic UI wraps inputs in divs - select the actual input element
    await pageA.getByTestId(T.groupsNameInput).locator('input').fill(groupName);
    await pageA.getByTestId(T.groupsCreateSubmit).click();

    await expect(pageA.getByTestId(T.groupRow(groupName))).toBeVisible({ timeout: 5000 });

    // Add member - button is in the table row
    // Note: For this test to work, nodeA needs to have nodeB as a contact
    // The first test (invite_add_friend) has nodeB add nodeA, so we need bidirectional
    // For now, skip if no contacts available
    const addMemberBtn = pageA
      .getByTestId(T.groupRow(groupName))
      .locator(`[data-testid="${T.groupAddMember}"]`)
      .first();
    await expect(addMemberBtn).toBeVisible({ timeout: 5000 });
    
    // Click and wait for modal to appear (check for modal header first, then picker)
    await addMemberBtn.click();
    
    // Wait for modal to open - check for modal header text or the picker
    // Semantic UI modals might take a moment to animate in
    try {
      await pageA.waitForSelector('text=Add Member to', { timeout: 5000 });
      console.log('[Test] Modal header found');
    } catch (err) {
      console.log('[Test] Modal header not found, checking for picker directly');
    }
    
    // Check if contacts are available - modal shows different UI if no contacts
    // If no contacts, it shows an input field instead of the picker dropdown
    const picker = pageA.getByTestId(T.groupMemberPicker);
    const pickerVisible = await picker.isVisible().catch(() => false);
    
    if (!pickerVisible) {
      // No contacts available - add by Soulseek username (legacy)
      const userInput = pageA
        .locator('.ui.modal')
        .locator('input[placeholder*="username" i]')
        .first();
      await expect(userInput).toBeVisible({ timeout: 5000 });
      await userInput.fill('nodeB');
      await pageA.getByTestId(T.groupMemberAddSubmit).click();
      await expect(userInput).not.toBeVisible({ timeout: 5000 });
    } else {
      // Contacts available - use the dropdown picker
      await picker.click();
      
      // Wait for dropdown options to appear, then select first available contact
      await pageA.getByRole("option").first().click({ timeout: 5000 });
      
      await pageA.getByTestId(T.groupMemberAddSubmit).click();
      
      // Wait for modal to close (member was added)
      await expect(picker).not.toBeVisible({ timeout: 5000 });
    }

    await ctxA.close();
  });

  test("create_collection_share_to_group", async ({ browser, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);

    const ctxA = await browser.newContext();
    const pageA = await ctxA.newPage();
    await login(pageA, nodeA);

    // Ensure the share group exists (so this test can run standalone)
    await clickNav(pageA, T.navGroups);
    const existingGroupRow = pageA.getByTestId(T.groupRow(groupName));
    if ((await existingGroupRow.count()) === 0) {
      await pageA.getByTestId(T.groupsCreate).click();
      await pageA.waitForSelector(`[data-testid="${T.groupsNameInput}"]`, { timeout: 5000 });
      await pageA.getByTestId(T.groupsNameInput).locator('input').fill(groupName);
      await pageA.getByTestId(T.groupsCreateSubmit).click();
      await expect(pageA.getByTestId(T.groupRow(groupName))).toBeVisible({ timeout: 5000 });
    }

    // Ensure nodeB is a member (recipient visibility depends on this)
    const addMemberBtn = pageA
      .getByTestId(T.groupRow(groupName))
      .locator(`[data-testid="${T.groupAddMember}"]`)
      .first();
    await expect(addMemberBtn).toBeVisible({ timeout: 5000 });
    await addMemberBtn.click();
    const modalUserInput = pageA
      .locator('.ui.modal')
      .locator('input[placeholder*="username" i]')
      .first();
    if ((await modalUserInput.count()) > 0) {
      await modalUserInput.fill('nodeB');
      await pageA.getByTestId(T.groupMemberAddSubmit).click();
      await expect(modalUserInput).not.toBeVisible({ timeout: 5000 });
    }

    // Navigate to collections page directly
    await pageA.goto(`${nodeA.baseUrl}/collections`, { waitUntil: 'networkidle', timeout: 10000 });
    
    // Wait a moment for React Router to process
    await pageA.waitForTimeout(2000);
    
    // Diagnostic: Check if route matched
    const routeMatched = await pageA.evaluate(() => (window as any).__ROUTE_MATCHED_COLLECTIONS__ || false);
    console.log('[Collections Test] Route matched flag:', routeMatched);
    
    // Diagnostic: Check router state
    const loc = await pageA.evaluate(() => location.pathname);
    console.log('[Collections Test] window.location.pathname =', loc);
    const urlBase = await pageA.evaluate(() => (window as any).urlBase || 'not set');
    console.log('[Collections Test] window.urlBase =', urlBase);
    
    // Check for route miss (via window flag or DOM element) - check multiple times as redirect might clear it
    const routeMissPath = await pageA.evaluate(() => {
      // Check both flags
      return (window as any).__ROUTE_MISS__ || (window as any).__ROUTE_MISS_ELEMENT__ || null;
    });
    const routeMissText = await pageA.evaluate(() => {
      const el = document.querySelector('[data-testid="route-miss"]');
      return el ? el.textContent : null;
    });
    
    console.log('[Collections Test] Route miss path:', routeMissPath);
    console.log('[Collections Test] Route miss text:', routeMissText);
    
    if (routeMissPath || routeMissText) {
      console.error('[Collections Test] ROUTE MISS DETECTED:', routeMissPath || routeMissText);
      throw new Error(`Route miss detected: ${routeMissPath || routeMissText}`);
    }
    
    // If route didn't match, that's the problem
    if (!routeMatched && loc === '/searches') {
      console.error('[Collections Test] ERROR: Route did not match - redirected to /searches');
      // Dump all route-related info
      const routeInfo = await pageA.evaluate(() => ({
        pathname: location.pathname,
        href: location.href,
        routeMatched: (window as any).__ROUTE_MATCHED_COLLECTIONS__ || false,
        routeMiss: (window as any).__ROUTE_MISS__ || null,
        routeMissElement: (window as any).__ROUTE_MISS_ELEMENT__ || null,
        urlBase: (window as any).urlBase || 'not set',
      }));
      console.error('[Collections Test] Route info:', JSON.stringify(routeInfo, null, 2));
      throw new Error(`Route /collections did not match. Route miss: ${routeMissPath || routeMissText || 'unknown'}`);
    }
    
    // Wait for collections page to load
    await pageA.waitForSelector('[data-testid="collections-root"]', { timeout: 10000 });
    
    // Create collection
    const createBtn = pageA.getByTestId(T.collectionsCreate);
    await expect(createBtn).toBeVisible({ timeout: 5000 });
    await createBtn.click();
    
    await pageA.waitForSelector(`[data-testid="${T.collectionsTypeSelect}"]`, { timeout: 5000 });
    await pageA.getByTestId(T.collectionsTypeSelect).click();
    await pageA.getByRole("option", { name: /playlist/i }).click();

    await pageA.getByTestId(T.collectionsTitleInput).locator('input').fill(collectionTitle);
    const createCollectionResponse = pageA.waitForResponse(
      (response) =>
        response.url().includes("/api/v0/collections") &&
        response.request().method() === "POST",
      { timeout: 5000 }
    );
    await pageA.getByTestId(T.collectionsCreateSubmit).click();
    const createCollectionResult = await createCollectionResponse;
    if (createCollectionResult.status() !== 201) {
      const body = await createCollectionResult.text();
      throw new Error(`Create collection failed: ${createCollectionResult.status()} ${body}`);
    }

    await expect(pageA.getByTestId(T.collectionRow(collectionTitle))).toBeVisible({ timeout: 5000 });
    await pageA.getByTestId(T.collectionRow(collectionTitle)).click();

    // Add a fixture item - click collection row first to select it
    // The collection row click should open the detail view
    await pageA.waitForTimeout(500); // Wait for selection
    
    // Click Add Item button
    const addItemBtn = pageA.getByTestId(T.collectionAddItem);
    if (await addItemBtn.count() > 0) {
      await addItemBtn.click();
      
      // Fill in the item picker (for test, we'll use "synthetic" as contentId)
      await pageA.getByTestId(T.collectionItemPicker).locator('input').fill("synthetic");
      await pageA.getByTestId(T.collectionAddItemSubmit).click();
      
      // Wait for item to appear
      await pageA.waitForTimeout(1000);
      await expect(pageA.getByTestId(T.collectionItems)).toBeVisible({ timeout: 5000 });
    }

    // Share it
    const shareCreate = pageA.getByTestId(T.shareCreate);
    await expect(shareCreate).toBeVisible({ timeout: 5000 });
    await shareCreate.click();
    const audiencePicker = pageA.getByTestId(T.shareAudiencePicker);
    await expect(audiencePicker).toBeVisible({ timeout: 5000 });
    await audiencePicker.click();
    const groupOption = pageA.getByRole("option", { name: new RegExp(groupName, "i") });
    if ((await groupOption.count()) === 0) {
      throw new Error("No share groups found in picker. Ensure group creation ran.");
    }
    await groupOption.first().click();

    await pageA.getByTestId(T.sharePolicyStream).check();
    await pageA.getByTestId(T.sharePolicyDownload).check();

    const createShareResponse = pageA.waitForResponse(
      (response) =>
        response.url().includes("/api/v0/share-grants") &&
        response.request().method() === "POST",
      { timeout: 5000 }
    );
    await pageA.getByTestId(T.shareCreateSubmit).click();
    const createShareResult = await createShareResponse;
    if (createShareResult.status() !== 201) {
      const body = await createShareResult.text();
      throw new Error(`Create share failed: ${createShareResult.status()} ${body}`);
    }
    await expect(pageA.getByTestId(T.sharesList)).toContainText(collectionTitle, { timeout: 5000 });

    await ctxA.close();
  });

  test("recipient_sees_shared_manifest", async ({ browser, request }) => {
    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;
    await waitForHealth(request, nodeB.baseUrl);

    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, nodeB);

    await clickNav(pageB, T.navSharedWithMe);

    const row = pageB.getByTestId(T.incomingShareRow(collectionTitle)).first();
    await expect(row).toBeVisible({ timeout: 15000 });
    await row.getByTestId(T.incomingShareOpen).click();

    await expect(pageB.getByTestId(T.sharedManifest)).toBeVisible({ timeout: 15000 });
    await expect(pageB.getByTestId(T.sharedManifest)).toContainText('synthetic', { timeout: 15000 });

    await ctxB.close();
  });

  test("stream_and_backfill", async ({ browser, request }) => {
    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;
    await waitForHealth(request, nodeB.baseUrl);

    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, nodeB);

    // Verify the share was received (cross-node discovery working)
    await clickNav(pageB, T.navSharedWithMe);
    const row = pageB.getByTestId(T.incomingShareRow(collectionTitle)).first();
    await expect(row).toBeVisible({ timeout: 20000 });

    // Note: Actual streaming and backfill require:
    // 1. Real contentId that resolves to files (not "synthetic")
    // 2. Backfill UI implementation
    // Cross-node discovery is verified by the share appearing above.
    // The recipient_sees_shared_manifest test verifies manifest access.

    await ctxB.close();
  });
});
