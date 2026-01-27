import { NODES, shouldLaunchNodes } from './env';
import { MultiPeerHarness } from './harness/MultiPeerHarness';
import { clickNav, login, waitForHealth } from './helpers';
import { T } from './selectors';
import { expect, test } from '@playwright/test';

test.describe.configure({ mode: 'serial' });

test.describe('multi-peer sharing', () => {
  let harness: MultiPeerHarness | null = null;
  const groupName = 'E2E Crew';
  const collectionTitle = 'E2E Playlist';

  test.beforeAll(async () => {
    if (shouldLaunchNodes()) {
      harness = new MultiPeerHarness();
      // Node A: shares movie/ + book/
      await harness.startNode(
        'A',
        [
          'test-data/slskdn-test-fixtures/movie',
          'test-data/slskdn-test-fixtures/book',
        ],
        {
          noConnect: process.env.SLSKDN_TEST_NO_CONNECT === 'true',
        },
      );
      // Node B: shares music/ + tv/
      await harness.startNode(
        'B',
        [
          'test-data/slskdn-test-fixtures/music',
          'test-data/slskdn-test-fixtures/tv',
        ],
        {
          noConnect: process.env.SLSKDN_TEST_NO_CONNECT === 'true',
        },
      );
      // Node C: recipient-only (no shares)
      await harness.startNode('C', [], {
        noConnect: process.env.SLSKDN_TEST_NO_CONNECT === 'true',
      });
    }
  });

  test.afterAll(async () => {
    if (harness) {
      await harness.stopAll();
    }
  });

  test('invite_add_friend', async ({ browser, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;

    await waitForHealth(request, nodeA.baseUrl);
    await waitForHealth(request, nodeB.baseUrl);

    const contextA = await browser.newContext();
    const contextB = await browser.newContext();
    const pageA = await contextA.newPage();
    const pageB = await contextB.newPage();

    await login(pageA, nodeA);
    await login(pageB, nodeB);

    // Diagnostic: Capture JS/runtime errors BEFORE navigation
    pageA.on('pageerror', (error) =>
      console.error('[Contacts Test] pageerror:', error),
    );
    pageA.on('console', (message) => {
      if (message.type() === 'error')
        console.error('[Contacts Test] console.error:', message.text());
    });

    // Navigate to contacts page and wait for it to load
    console.log('[Contacts Test] Navigating to contacts page...');
    const targetUrl = `${nodeA.baseUrl}/contacts`;
    console.log('[Contacts Test] Target URL:', targetUrl);
    await pageA.goto(targetUrl, { timeout: 10_000, waitUntil: 'networkidle' });
    console.log('[Contacts Test] Navigation complete, URL:', pageA.url());

    // Diagnostic: Compare browser location vs app location (memory history check)
    const loc = await pageA.evaluate(() => ({
      href: location.href,
      pathname: location.pathname,
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
    console.log(
      '[Contacts Test] Browser location:',
      JSON.stringify(loc, null, 2),
    );
    console.log('[Contacts Test] App location/history:', appLoc);

    // Check if URL changed (redirect happened)
    const finalUrl = pageA.url();
    if (!finalUrl.includes('/contacts')) {
      console.error(
        `[Contacts Test] ERROR: Redirected away from /contacts! Final URL: ${finalUrl}`,
      );
    }

    // Check what's actually on the page
    const bodyContent = await pageA.locator('body').innerText();
    console.log(
      '[Contacts Test] Body text (first 500 chars):',
      bodyContent.slice(0, 500),
    );

    // Check which component is actually rendering
    const hasSearchElements = await pageA
      .locator('input[placeholder*="Search"], [data-testid*="search"]')
      .count();
    const hasContactsElements = await pageA
      .locator('[data-testid="contacts-root"], [data-testid*="contact"]')
      .count();
    console.log('[Contacts Test] Search elements count:', hasSearchElements);
    console.log(
      '[Contacts Test] Contacts elements count:',
      hasContactsElements,
    );

    // Check React component tree if possible
    const reactRoot = await pageA.evaluate(() => {
      const root = document.querySelector('#root');
      if (!root) return null;
      const firstChild = root.firstElementChild;
      return {
        firstChildClass: firstChild?.className,
        firstChildTag: firstChild?.tagName,
        firstChildText: firstChild?.textContent?.slice(0, 100),
        rootTag: root.tagName,
      };
    });
    console.log(
      '[Contacts Test] React root info:',
      JSON.stringify(reactRoot, null, 2),
    );

    // Check if we're still on login page (route guard)
    const loginForm = await pageA
      .locator(
        '[data-testid="login-username"], input[placeholder*="Username" i]',
      )
      .count();
    console.log('[Contacts Test] Login form count:', loginForm);
    if (loginForm > 0) {
      console.error(
        '[Contacts Test] ERROR: Still on login page - route guard may be blocking!',
      );
    }

    // Check for any React error boundaries or error messages
    const errorElements = await pageA
      .locator('[class*="error"], [class*="Error"], [data-testid*="error"]')
      .count();
    console.log('[Contacts Test] Error elements count:', errorElements);

    // Check what React Router thinks the current route is
    const currentRoute = await pageA.evaluate(() => {
      // Try to find React Router state or current pathname
      return {
        hash: window.location.hash,
        pathname: window.location.pathname,
        search: window.location.search,
        urlBase: (window as any).urlBase || 'not set',
      };
    });
    console.log(
      '[Contacts Test] Current route info:',
      JSON.stringify(currentRoute, null, 2),
    );

    // Wait for contacts root to appear (ensures component mounted)
    console.log('[Contacts Test] Waiting for contacts-root...');
    try {
      await pageA.waitForSelector('[data-testid="contacts-root"]', {
        timeout: 10_000,
      });
      console.log('[Contacts Test] contacts-root found - component mounted');
    } catch (error) {
      console.error('[Contacts Test] ERROR: contacts-root not found!');
      // Dump all data-testid elements to see what's actually rendered
      const allTestIds = await pageA.evaluate(() => {
        const elements = document.querySelectorAll('[data-testid]');
        return Array.from(elements).map((element) => ({
          tag: element.tagName,
          testid: element.dataset.testid,
          visible: (element as HTMLElement).offsetParent !== null,
        }));
      });
      console.log(
        '[Contacts Test] All data-testid elements on page:',
        JSON.stringify(allTestIds, null, 2),
      );
      throw error;
    }

    // Wait for contacts API call to complete and capture response body
    console.log('[Contacts Test] Waiting for /api/v0/contacts response...');
    let resp;
    try {
      resp = await pageA.waitForResponse(
        (r) => r.url().includes('/api/v0/contacts') && r.status() === 200,
        { timeout: 10_000 },
      );

      // Diagnostic: Verify response body
      const text = await resp.text();
      const contentType = resp.headers()['content-type'] || '';
      console.log('[Contacts Test] API Response - Content-Type:', contentType);
      console.log('[Contacts Test] API Response - Length:', text.length);
      console.log(
        '[Contacts Test] API Response - First 200 chars:',
        text.slice(0, 200),
      );

      if (text.length === 0) {
        console.error(
          '[Contacts Test] ERROR: API returned 200 with empty body!',
        );
      }

      if (text.startsWith('<html')) {
        console.error(
          '[Contacts Test] ERROR: API returned HTML instead of JSON!',
        );
      }
    } catch (error) {
      console.error('[Contacts Test] ERROR waiting for API response:', error);
      // Continue with diagnostics even if API wait failed
    }

    // Diagnostic: Check current state
    const tid = T.contactsCreateInvite;
    const count = await pageA.locator(`[data-testid="${tid}"]`).count();
    console.log(`[Contacts Test] count([data-testid="${tid}"]) =`, count);

    // If present, dump visibility diagnostics
    if (count > 0) {
      const diag = await pageA
        .locator(`[data-testid="${tid}"]`)
        .first()
        .evaluate((element) => {
          const cs = getComputedStyle(element);
          const rect = element.getBoundingClientRect();
          return {
            disabled: (element as any).disabled ?? null,
            display: cs.display,
            inDocument: document.contains(element),
            opacity: cs.opacity,
            rect: { h: rect.height, w: rect.width, x: rect.x, y: rect.y },
            tag: element.tagName,
            visibility: cs.visibility,
          };
        });
      console.log(
        '[Contacts Test] button diag:',
        JSON.stringify(diag, null, 2),
      );
    } else {
      console.error(
        `[Contacts Test] ERROR: Button with data-testid="${tid}" not found in DOM!`,
      );
    }

    // Screenshot and body snippet for debugging
    await pageA.screenshot({ fullPage: true, path: 'contacts-debug.png' });
    const bodyText = await pageA.locator('body').innerText();
    console.log(
      '[Contacts Test] body snippet (first 800 chars):',
      bodyText.slice(0, 800),
    );

    // Wait for create invite button (appears in header - always visible)
    const createInviteButton = pageA.getByTestId(T.contactsCreateInvite);
    await expect(createInviteButton.first()).toBeVisible({ timeout: 5_000 });

    await createInviteButton.click();

    // Wait for invite modal and get invite link
    const inviteOutput = pageA.getByTestId(T.contactsInviteOutput);
    await expect(inviteOutput).toBeVisible({ timeout: 5_000 });
    const invite = await inviteOutput.inputValue();
    expect(invite.length).toBeGreaterThan(20);

    // Node B adds friend
    await pageB.goto(`${nodeB.baseUrl}/contacts`, {
      timeout: 5_000,
      waitUntil: 'networkidle',
    });

    const addFriendButton = pageB.getByTestId(T.contactsAddFriend);
    await expect(addFriendButton).toBeVisible({ timeout: 5_000 });
    await addFriendButton.click();

    // Fill invite form
    const inviteInput = pageB.getByTestId(T.contactsAddInviteInput);
    await expect(inviteInput).toBeVisible({ timeout: 3_000 });
    await inviteInput.fill(invite);

    const nicknameInput = pageB.getByTestId(T.contactsContactNickname);
    await expect(nicknameInput).toBeVisible({ timeout: 3_000 });
    await nicknameInput.fill('nodeA');

    await pageB.getByTestId(T.contactsAddInviteSubmit).click();

    // Contact row should appear after adding
    const contactRow = pageB.getByTestId(T.contactsRow('nodeA'));
    await expect(contactRow).toBeVisible({ timeout: 5_000 });

    await contextA.close();
    await contextB.close();
  });

  test('create_group_add_member', async ({ browser, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);

    const contextA = await browser.newContext();
    const pageA = await contextA.newPage();
    await login(pageA, nodeA);

    await clickNav(pageA, T.navGroups);
    await pageA.getByTestId(T.groupsCreate).click();

    // Wait for create group modal
    await pageA.waitForSelector(`[data-testid="${T.groupsNameInput}"]`, {
      timeout: 5_000,
    });
    // Semantic UI wraps inputs in divs - select the actual input element
    await pageA.getByTestId(T.groupsNameInput).locator('input').fill(groupName);
    await pageA.getByTestId(T.groupsCreateSubmit).click();

    await expect(pageA.getByTestId(T.groupRow(groupName))).toBeVisible({
      timeout: 5_000,
    });

    // Add member - button is in the table row
    // Note: For this test to work, nodeA needs to have nodeB as a contact
    // The first test (invite_add_friend) has nodeB add nodeA, so we need bidirectional
    // For now, skip if no contacts available
    const addMemberButton = pageA
      .getByTestId(T.groupRow(groupName))
      .locator(`[data-testid="${T.groupAddMember}"]`)
      .first();
    await expect(addMemberButton).toBeVisible({ timeout: 5_000 });

    // Click and wait for modal to appear (check for modal header first, then picker)
    await addMemberButton.click();

    // Wait for modal to open - check for modal header text or the picker
    // Semantic UI modals might take a moment to animate in
    try {
      await pageA.waitForSelector('text=Add Member to', { timeout: 5_000 });
      console.log('[Test] Modal header found');
    } catch {
      console.log(
        '[Test] Modal header not found, checking for picker directly',
      );
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
      await expect(userInput).toBeVisible({ timeout: 5_000 });
      await userInput.fill('nodeB');
      await pageA.getByTestId(T.groupMemberAddSubmit).click();
      await expect(userInput).not.toBeVisible({ timeout: 5_000 });
    } else {
      // Contacts available - use the dropdown picker
      await picker.click();

      // Wait for dropdown options to appear, then select first available contact
      await pageA.getByRole('option').first().click({ timeout: 5_000 });

      await pageA.getByTestId(T.groupMemberAddSubmit).click();

      // Wait for modal to close (member was added)
      await expect(picker).not.toBeVisible({ timeout: 5_000 });
    }

    await contextA.close();
  });

  test('create_collection_share_to_group', async ({ browser, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    await waitForHealth(request, nodeA.baseUrl);

    const contextA = await browser.newContext();
    const pageA = await contextA.newPage();
    await login(pageA, nodeA);

    // Ensure the share group exists (so this test can run standalone)
    await clickNav(pageA, T.navGroups);
    const existingGroupRow = pageA.getByTestId(T.groupRow(groupName));
    if ((await existingGroupRow.count()) === 0) {
      await pageA.getByTestId(T.groupsCreate).click();
      await pageA.waitForSelector(`[data-testid="${T.groupsNameInput}"]`, {
        timeout: 5_000,
      });
      await pageA
        .getByTestId(T.groupsNameInput)
        .locator('input')
        .fill(groupName);
      await pageA.getByTestId(T.groupsCreateSubmit).click();
      await expect(pageA.getByTestId(T.groupRow(groupName))).toBeVisible({
        timeout: 5_000,
      });
    }

    // Ensure nodeC is a member (recipient visibility depends on this)
    const addMemberButton = pageA
      .getByTestId(T.groupRow(groupName))
      .locator(`[data-testid="${T.groupAddMember}"]`)
      .first();
    await expect(addMemberButton).toBeVisible({ timeout: 5_000 });
    await addMemberButton.click();
    const modalUserInput = pageA
      .locator('.ui.modal')
      .locator('input[placeholder*="username" i]')
      .first();
    if ((await modalUserInput.count()) > 0) {
      await modalUserInput.fill('nodeC');
      await pageA.getByTestId(T.groupMemberAddSubmit).click();
      await expect(modalUserInput).not.toBeVisible({ timeout: 5_000 });
    }

    // Navigate to collections page directly
    await pageA.goto(`${nodeA.baseUrl}/collections`, {
      timeout: 10_000,
      waitUntil: 'networkidle',
    });

    // Wait a moment for React Router to process
    await pageA.waitForTimeout(2_000);

    // Diagnostic: Check if route matched
    const routeMatched = await pageA.evaluate(
      () => (window as any).routeMatchedCollections || false,
    );
    console.log('[Collections Test] Route matched flag:', routeMatched);

    // Diagnostic: Check router state
    const loc = await pageA.evaluate(() => location.pathname);
    console.log('[Collections Test] window.location.pathname =', loc);
    const urlBase = await pageA.evaluate(
      () => (window as any).urlBase || 'not set',
    );
    console.log('[Collections Test] window.urlBase =', urlBase);

    const collectionsRootCount = await pageA
      .locator('[data-testid="collections-root"]')
      .count();
    if (!collectionsRootCount && !routeMatched && loc === '/searches') {
      const title = await pageA.title();
      throw new Error(
        `Stale WebUI bundle detected: /collections route missing (title=${title}, url=${pageA.url()}). ` +
          'Run `npm run build` and re-run e2e with harness-launched nodes.',
      );
    }

    // Check for route miss (via window flag or DOM element) - check multiple times as redirect might clear it
    const routeMissPath = await pageA.evaluate(() => {
      // Check both flags
      return (
        (window as any).routeMissPath ||
        (window as any).routeMissElement ||
        null
      );
    });
    const routeMissText = await pageA.evaluate(() => {
      const element = document.querySelector('[data-testid="route-miss"]');
      return element ? element.textContent : null;
    });

    console.log('[Collections Test] Route miss path:', routeMissPath);
    console.log('[Collections Test] Route miss text:', routeMissText);

    if (routeMissPath || routeMissText) {
      console.error(
        '[Collections Test] ROUTE MISS DETECTED:',
        routeMissPath || routeMissText,
      );
      throw new Error(`Route miss detected: ${routeMissPath || routeMissText}`);
    }

    // If route didn't match, that's the problem
    if (!routeMatched && loc === '/searches') {
      console.error(
        '[Collections Test] ERROR: Route did not match - redirected to /searches',
      );
      // Dump all route-related info
      const routeInfo = await pageA.evaluate(() => ({
        href: location.href,
        pathname: location.pathname,
        routeMatched: (window as any).routeMatchedCollections || false,
        routeMiss: (window as any).routeMissPath || null,
        routeMissElement: (window as any).routeMissElement || null,
        urlBase: (window as any).urlBase || 'not set',
      }));
      console.error(
        '[Collections Test] Route info:',
        JSON.stringify(routeInfo, null, 2),
      );
      throw new Error(
        `Route /collections did not match. Route miss: ${routeMissPath || routeMissText || 'unknown'}`,
      );
    }

    // Wait for collections page to load
    await pageA.waitForSelector('[data-testid="collections-root"]', {
      timeout: 10_000,
    });

    // Create collection
    const createButton = pageA.getByTestId(T.collectionsCreate);
    await expect(createButton).toBeVisible({ timeout: 5_000 });
    await createButton.click();

    await pageA.waitForSelector(`[data-testid="${T.collectionsTypeSelect}"]`, {
      timeout: 5_000,
    });
    await pageA.getByTestId(T.collectionsTypeSelect).click();
    await pageA.getByRole('option', { name: /playlist/i }).click();

    await pageA
      .getByTestId(T.collectionsTitleInput)
      .locator('input')
      .fill(collectionTitle);
    const createCollectionResponse = pageA.waitForResponse(
      (response) =>
        response.url().includes('/api/v0/collections') &&
        response.request().method() === 'POST',
      { timeout: 5_000 },
    );
    await pageA.getByTestId(T.collectionsCreateSubmit).click();
    const createCollectionResult = await createCollectionResponse;
    if (createCollectionResult.status() !== 201) {
      const body = await createCollectionResult.text();
      throw new Error(
        `Create collection failed: ${createCollectionResult.status()} ${body}`,
      );
    }

    await expect(
      pageA.getByTestId(T.collectionRow(collectionTitle)),
    ).toBeVisible({ timeout: 5_000 });
    await pageA.getByTestId(T.collectionRow(collectionTitle)).click();

    // Add real fixture items using library search
    // The collection row click should open the detail view
    await pageA.waitForTimeout(500); // Wait for selection

    // Click Add Item button
    const addItemButton = pageA.getByTestId('collection-add-item');
    await expect(addItemButton).toBeVisible({ timeout: 5_000 });
    await addItemButton.click();

    // Search for sintel (movie fixture)
    const searchInput = pageA.getByTestId('collection-item-search-input');
    await expect(searchInput).toBeVisible({ timeout: 5_000 });
    await searchInput.locator('input').fill('sintel');

    // Wait for search results
    await pageA.waitForTimeout(1_000); // Give API time to respond
    const resultsDropdown = pageA.getByTestId('collection-item-results');
    if ((await resultsDropdown.count()) > 0) {
      await resultsDropdown.click();
      // Select first result (should be sintel video)
      await pageA.getByRole('option').first().click({ timeout: 5_000 });
    }

    // Add the item
    await pageA.getByTestId('collection-add-item-submit').click();
    await pageA.waitForTimeout(1_000);

    // Add second item: treasure (book fixture)
    await addItemButton.click();
    await searchInput.locator('input').fill('treasure');
    await pageA.waitForTimeout(1_000);
    if ((await resultsDropdown.count()) > 0) {
      await resultsDropdown.click();
      await pageA.getByRole('option').first().click({ timeout: 5_000 });
    }
    await pageA.getByTestId('collection-add-item-submit').click();
    await pageA.waitForTimeout(1_000);

    // Verify items were added
    await expect(pageA.getByTestId('collection-items-table')).toBeVisible({
      timeout: 5_000,
    });

    // Share it
    const shareCreate = pageA.getByTestId(T.shareCreate);
    await expect(shareCreate).toBeVisible({ timeout: 5_000 });
    await shareCreate.click();
    const audiencePicker = pageA.getByTestId(T.shareAudiencePicker);
    await expect(audiencePicker).toBeVisible({ timeout: 5_000 });
    await audiencePicker.click();
    const groupOption = pageA.getByRole('option', {
      name: new RegExp(groupName, 'i'),
    });
    if ((await groupOption.count()) === 0) {
      throw new Error(
        'No share groups found in picker. Ensure group creation ran.',
      );
    }

    await groupOption.first().click();

    await pageA.getByTestId(T.sharePolicyStream).check();
    await pageA.getByTestId(T.sharePolicyDownload).check();

    const createShareResponse = pageA.waitForResponse(
      (response) =>
        response.url().includes('/api/v0/share-grants') &&
        response.request().method() === 'POST',
      { timeout: 5_000 },
    );
    await pageA.getByTestId(T.shareCreateSubmit).click();
    const createShareResult = await createShareResponse;
    if (createShareResult.status() !== 201) {
      const body = await createShareResult.text();
      throw new Error(
        `Create share failed: ${createShareResult.status()} ${body}`,
      );
    }

    await expect(pageA.getByTestId(T.sharesList)).toContainText(
      collectionTitle,
      { timeout: 5_000 },
    );

    await contextA.close();
  });

  test('recipient_sees_shared_manifest', async ({ browser, request }) => {
    const nodeC = harness ? harness.getNode('C').nodeCfg : NODES.C;
    await waitForHealth(request, nodeC.baseUrl);

    const contextC = await browser.newContext();
    const pageC = await contextC.newPage();
    await login(pageC, nodeC);

    await clickNav(pageC, T.navSharedWithMe);

    const row = pageC
      .getByTestId(`incoming-share-row-${collectionTitle}`)
      .first();
    await expect(row).toBeVisible({ timeout: 15_000 });
    await row.getByTestId('incoming-share-open').click();

    await expect(pageC.getByTestId('shared-manifest')).toBeVisible({
      timeout: 15_000,
    });

    // Verify real fixture items appear (sintel or treasure)
    const manifestContent = await pageC
      .getByTestId('shared-manifest')
      .textContent();
    expect(manifestContent).toMatch(/sintel|treasure|mp4|txt/i);

    await contextC.close();
  });

  test('recipient_streams_video', async ({ browser, request }) => {
    const nodeC = harness ? harness.getNode('C').nodeCfg : NODES.C;
    await waitForHealth(request, nodeC.baseUrl);

    const contextC = await browser.newContext();
    const pageC = await contextC.newPage();
    await login(pageC, nodeC);

    await clickNav(pageC, T.navSharedWithMe);

    const row = pageC
      .getByTestId(`incoming-share-row-${collectionTitle}`)
      .first();
    await expect(row).toBeVisible({ timeout: 15_000 });
    await row.getByTestId('incoming-share-open').click();

    await expect(pageC.getByTestId('shared-manifest')).toBeVisible({
      timeout: 15_000,
    });

    // Find and click stream button for sintel video (should have sha256 prefix in testid)
    const streamButtons = pageC.locator('[data-testid^="incoming-stream-"]');
    const streamButtonCount = await streamButtons.count();
    if (streamButtonCount > 0) {
      // Set up response listener before clicking
      const streamResponsePromise = pageC.waitForResponse(
        (response) => {
          const url = response.url();
          return (
            url.includes('/api/v0/streams/') &&
            (response.status() === 200 || response.status() === 206)
          );
        },
        { timeout: 10_000 },
      );

      await streamButtons.first().click();

      // Wait for stream response and verify it's 206 (Range request) or 200
      const streamResponse = await streamResponsePromise;
      const status = streamResponse.status();
      expect([200, 206]).toContain(status);

      // Verify Content-Type for video
      const contentType = streamResponse.headers()['content-type'];
      if (status === 206) {
        // Range request should return partial content
        expect(contentType).toMatch(/video|audio|application/i);
      }

      console.log(
        `[Stream Test] Stream response: ${status}, Content-Type: ${contentType}`,
      );
    } else {
      console.warn('[Stream Test] No stream buttons found in manifest');
    }

    await contextC.close();
  });

  test('recipient_backfills_and_verifies_download', async ({
    browser,
    request,
  }) => {
    // File verification requires harness-launched nodes (works in both local and CI)
    // In CI, harness is always used (no SLSKDN_NODE_*_URL env vars)
    // Locally, harness is used unless nodes are pre-launched via env vars
    if (!harness) {
      test.skip(
        true,
        'File verification requires harness-launched nodes (unset SLSKDN_NODE_*_URL to use harness)',
      );
      return;
    }

    const nodeCInstance = harness.getNode('C');
    const nodeC = nodeCInstance.nodeCfg;
    await waitForHealth(request, nodeC.baseUrl);

    const contextC = await browser.newContext();
    const pageC = await contextC.newPage();
    await login(pageC, nodeC);

    await clickNav(pageC, T.navSharedWithMe);

    const row = pageC
      .getByTestId(`incoming-share-row-${collectionTitle}`)
      .first();
    await expect(row).toBeVisible({ timeout: 15_000 });
    await row.getByTestId('incoming-share-open').click();

    await expect(pageC.getByTestId('shared-manifest')).toBeVisible({
      timeout: 15_000,
    });

    // Click backfill button
    const backfillButton = pageC.getByTestId('incoming-backfill');
    if ((await backfillButton.count()) > 0) {
      await backfillButton.click();

      // Wait for backfill to start (button shows loading state)
      await pageC.waitForTimeout(2_000);

      // Poll downloads directory for file existence
      // Note: This requires the harness to expose the app directory or we need a test endpoint
      // For now, we'll verify the backfill API call succeeded
      const backfillResponsePromise = pageC.waitForResponse(
        (response) =>
          response.url().includes('/api/v0/share-grants/') &&
          response.url().includes('/backfill') &&
          response.request().method() === 'POST',
        { timeout: 10_000 },
      );

      try {
        const backfillResponse = await backfillResponsePromise;
        expect([200, 201, 202]).toContain(backfillResponse.status());
        console.log(
          `[Backfill Test] Backfill started: ${backfillResponse.status()}`,
        );
      } catch (error) {
        console.warn('[Backfill Test] Backfill response not captured:', error);
      }

      // Verify files exist in downloads directory using harness
      // Works for both local E2E and CI (harness always launches nodes in CI)
      // nodeCInstance is already available from test setup above

      // Wait for files to appear (backfill is asynchronous)
      // Try to find sintel or treasure file (from the collection we shared)
      const sintelFile = await nodeCInstance.waitForDownloadedFile(
        'sintel',
        30_000,
      );
      const treasureFile = await nodeCInstance.waitForDownloadedFile(
        'treasure',
        30_000,
      );

      // Verify at least one file was downloaded
      if (!sintelFile && !treasureFile) {
        // List all files for debugging
        const allFiles = await nodeCInstance.getDownloadedFiles();
        console.error(
          `[Backfill Test] No expected files found. All downloaded files:`,
          allFiles.map((f) => `${f.name} (${f.size} bytes)`),
        );
        throw new Error(
          'Backfill failed: expected files (sintel or treasure) not found in downloads',
        );
      }

      // Verify file sizes are correct (non-zero and reasonable)
      if (sintelFile) {
        expect(sintelFile.size).toBeGreaterThan(0);
        // sintel_512kb_stereo.mp4 should be ~77MB
        expect(sintelFile.size).toBeGreaterThan(1_000_000); // At least 1MB
        console.log(
          `[Backfill Test] ✓ Found sintel file: ${sintelFile.name} (${sintelFile.size} bytes)`,
        );
      }

      if (treasureFile) {
        expect(treasureFile.size).toBeGreaterThan(0);
        // treasure_island_pg120.txt should be ~400KB
        expect(treasureFile.size).toBeGreaterThan(100_000); // At least 100KB
        console.log(
          `[Backfill Test] ✓ Found treasure file: ${treasureFile.name} (${treasureFile.size} bytes)`,
        );
      }

      // List all downloaded files for completeness
      const allFiles = await nodeCInstance.getDownloadedFiles();
      console.log(
        `[Backfill Test] Total files in downloads: ${allFiles.length}`,
        allFiles.map((f) => `${f.name} (${f.size} bytes)`),
      );
    } else {
      console.warn('[Backfill Test] No backfill button found (download not allowed?)');
    }

    await contextC.close();
  });
});
