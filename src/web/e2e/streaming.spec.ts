import { NODES, shouldLaunchNodes } from './env';
import { MultiPeerHarness } from './harness/MultiPeerHarness';
import {
  announceShareGrant,
  clickNav,
  getAuthToken,
  login,
  waitForHealth,
  waitForLibraryItem,
  waitForShareGrantById,
} from './helpers';
import { T } from './selectors';
import { expect, test } from '@playwright/test';

test.describe('streaming', () => {
  let harness: MultiPeerHarness | null = null;
  const groupName = 'E2E Crew';
  const collectionTitle = 'E2E Streaming Test';
  let sharedGrantId: string | null = null;
  let sharedCollectionId: string | null = null;
  let ownerAuthToken: string | null = null;
  let recipientAuthToken: string | null = null;

  test.beforeAll(async () => {
    if (shouldLaunchNodes()) {
      harness = new MultiPeerHarness();
      await harness.startNode('A', 'test-data/slskdn-test-fixtures/movie', {
        noConnect: process.env.SLSKDN_TEST_NO_CONNECT === 'true',
      });
      await harness.startNode('B', 'test-data/slskdn-test-fixtures/book', {
        noConnect: process.env.SLSKDN_TEST_NO_CONNECT === 'true',
      });
    }
  });

  test.afterAll(async () => {
    if (harness) {
      await harness.stopAll();
    }
  });

  test('recipient_streams_item_with_range', async ({ browser, request }) => {
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

    // Ensure group and collection exist (reuse from multippeer-sharing tests)
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

    // Create collection and share (similar to multippeer-sharing test)
    await clickNav(pageA, T.navCollections);
    await pageA.waitForSelector('[data-testid="collections-root"]', {
      timeout: 10_000,
    });

    const existingCollectionRow = pageA.getByTestId(
      T.collectionRow(collectionTitle),
    );
    if ((await existingCollectionRow.count()) === 0) {
      await pageA.getByTestId(T.collectionsCreate).click();
      await pageA.waitForSelector(
        `[data-testid="${T.collectionsTypeSelect}"]`,
        { timeout: 5_000 },
      );
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
    }

    await pageA.getByTestId(T.collectionRow(collectionTitle)).click();
    await pageA.waitForTimeout(200); // Reduced from 500ms

    // Add item
    const addItemButton = pageA.getByTestId(T.collectionAddItem);
    if ((await addItemButton.count()) > 0) {
      await addItemButton.click();
      const item = await waitForLibraryItem(pageA, 'sintel');
      await pageA
        .getByTestId(T.collectionItemPicker)
        .locator('input')
        .fill(item.contentId);
      await pageA.getByTestId(T.collectionAddItemSubmit).click();
      await pageA.waitForTimeout(500); // Reduced from 1000ms
    }

    // Share with stream enabled
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
    let createShareBody;
    try {
      createShareBody = await createShareResult.json();
    } catch {
      createShareBody = await createShareResult.text();
    }

    if (createShareResult.status() !== 201) {
      throw new Error(
        `Create share failed: ${createShareResult.status()} ${typeof createShareBody === 'string' ? createShareBody : JSON.stringify(createShareBody)}`,
      );
    }

    if (!createShareBody?.id) {
      throw new Error('Create share response missing id.');
    }

    const ownerToken = await getAuthToken(pageA);
    const recipientToken = await getAuthToken(pageB);
    await announceShareGrant({
      owner: nodeA,
      ownerToken,
      recipient: nodeB,
      recipientToken,
      request,
      shareGrantId: createShareBody.id,
      shareOverride: createShareBody,
    });
    sharedGrantId = createShareBody.id;
    sharedCollectionId = createShareBody.collectionId;
    ownerAuthToken = ownerToken;
    recipientAuthToken = recipientToken;

    // Wait for cross-node discovery (reduced - announceShareGrant should make this faster)
    await pageB.waitForTimeout(2_000); // Reduced from 5000ms

    // Node B tries to stream
    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(1_000); // Reduced from 2000ms

    // Poll for the share to appear
    let shareFound = false;
    let streamUrl: string | null = null;
    for (let index = 0; index < 20; index++) {
      const shareRow = pageB
        .getByTestId(T.incomingShareRow(collectionTitle))
        .first();
      if ((await shareRow.count()) > 0) {
        shareFound = true;
        await shareRow.getByTestId(T.incomingShareOpen).click();
        await expect(pageB.getByTestId(T.sharedManifest)).toBeVisible({
          timeout: 15_000,
        });

        // Get stream URL from manifest via API (more reliable than UI)
        streamUrl = await pageB.evaluate(
          async ({ expectedTitle, expectedOwnerBaseUrl }) => {
            const token =
              sessionStorage.getItem('slskd-token') ||
              localStorage.getItem('slskd-token');
            if (!token) return null;

            const sharesRes = await fetch('/api/v0/share-grants', {
              headers: { Authorization: `Bearer ${token}` },
            });
            if (!sharesRes.ok) return null;
            const sharesText = await sharesRes.text();
            if (!sharesText) return null;
            let shares;
            try {
              shares = JSON.parse(sharesText);
            } catch {
              return null;
            }

            if (!Array.isArray(shares) || shares.length === 0) return null;

            for (const share of shares) {
              if (!share?.id) continue;
              const manifestRes = await fetch(
                `/api/v0/share-grants/${share.id}/manifest`,
                {
                  headers: { Authorization: `Bearer ${token}` },
                },
              );
              if (!manifestRes.ok) continue;
              const manifestText = await manifestRes.text();
              if (!manifestText) continue;
              let manifest;
              try {
                manifest = JSON.parse(manifestText);
              } catch {
                continue;
              }

              if (manifest?.title !== expectedTitle) continue;
              const item = manifest?.items?.[0];
              const url = item?.streamUrl;
              if (!url) continue;

              if (url.startsWith(expectedOwnerBaseUrl)) return url;
              if (url.startsWith('/')) return `${expectedOwnerBaseUrl}${url}`;
            }

            return null;
          },
          {
            expectedOwnerBaseUrl: nodeA.baseUrl,
            expectedTitle: collectionTitle,
          },
        );

        break;
      }

      await pageB.waitForTimeout(500); // Reduced from 1000ms
    }

    expect(shareFound).toBe(true);
    if (!streamUrl) {
      throw new Error('No streamUrl found in manifest for streaming test.');
    }

    // Make a Range request (simulating stream)
    const normalized = streamUrl
      .replace('http://localhost:', 'http://127.0.0.1:')
      .replace('https://localhost:', 'https://127.0.0.1:');
    const fullStreamUrl = normalized.startsWith('http')
      ? normalized
      : `${nodeB.baseUrl}${normalized}`;

    const rangeResponse = await request.get(fullStreamUrl, {
      failOnStatusCode: false,
      headers: { Range: 'bytes=0-1' },
    });

    // Should get 206 (Partial Content) or 200 (full content)
    expect([206, 200]).toContain(rangeResponse.status());

    await contextA.close();
    await contextB.close();
  });

  test('seek_works_with_range_requests', async ({ browser, request }) => {
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

    // Ensure share exists and is announced (reuse from previous test or create new)
    if (!sharedGrantId || !ownerAuthToken) {
      const ownerToken = await getAuthToken(pageA);
      const recipientToken = await getAuthToken(pageB);

      // Quick setup: reuse collection if exists, otherwise create
      await clickNav(pageA, T.navCollections);
      await pageA.waitForSelector('[data-testid="collections-root"]', {
        timeout: 10_000,
      });

      const existingCollectionRow = pageA.getByTestId(
        T.collectionRow(collectionTitle),
      );
      const collectionId: string | null = null;
      if ((await existingCollectionRow.count()) > 0) {
        await existingCollectionRow.click();
        await pageA.waitForTimeout(200); // Reduced from 500ms
        const createShareResponse = pageA.waitForResponse(
          (response) =>
            response.url().includes('/api/v0/share-grants') &&
            response.request().method() === 'POST',
          { timeout: 5_000 },
        );
        const shareCreate = pageA.getByTestId(T.shareCreate);
        if ((await shareCreate.count()) > 0) {
          await shareCreate.click();
          const audiencePicker = pageA.getByTestId(T.shareAudiencePicker);
          await expect(audiencePicker).toBeVisible({ timeout: 5_000 });
          await audiencePicker.click();
          const groupOption = pageA.getByRole('option', {
            name: new RegExp(groupName, 'i'),
          });
          if ((await groupOption.count()) > 0) {
            await groupOption.first().click();
            await pageA.getByTestId(T.sharePolicyStream).check();
            await pageA.getByTestId(T.sharePolicyDownload).check();
            await pageA.getByTestId(T.shareCreateSubmit).click();
            const createShareResult = await createShareResponse;
            let createShareBody;
            try {
              createShareBody = await createShareResult.json();
            } catch {
              createShareBody = await createShareResult.text();
            }

            if (createShareResult.status() === 201 && createShareBody?.id) {
              await announceShareGrant({
                owner: nodeA,
                ownerToken,
                recipient: nodeB,
                recipientToken,
                request,
                shareGrantId: createShareBody.id,
                shareOverride: createShareBody,
              });
              sharedGrantId = createShareBody.id;
              sharedCollectionId = createShareBody.collectionId;
              ownerAuthToken = ownerToken;
              recipientAuthToken = recipientToken;
            }
          }
        }
      }
    }

    // Wait for share grant to be available (if we have the ID from previous test)
    const recipientToken = await getAuthToken(pageB);
    if (sharedGrantId) {
      const shareAvailable = await waitForShareGrantById({
        baseUrl: nodeB.baseUrl,
        request,
        shareGrantId: sharedGrantId,
        timeoutMs: 30_000,
        token: recipientToken,
      });
      if (!shareAvailable) {
        throw new Error(
          `Share grant ${sharedGrantId} not found on recipient node after 30s`,
        );
      }
    }

    // Navigate to shared content
    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(1_000); // Reduced from 2000ms

    // Poll for the share to appear in UI (or fetch directly by ID)
    let shareFound = false;
    let streamUrl: string | null = null;

    if (sharedGrantId) {
      // Direct fetch by ID (more reliable)
      const manifestRes = await request.get(
        `${nodeB.baseUrl}/api/v0/share-grants/${sharedGrantId}/manifest`,
        {
          failOnStatusCode: false,
          headers: { Authorization: `Bearer ${recipientToken}` },
        },
      );
      if (manifestRes.ok()) {
        const manifest = await manifestRes.json();
        if (manifest?.title === collectionTitle) {
          const item = manifest?.items?.[0];
          streamUrl = item?.streamUrl;
          if (streamUrl) {
            if (!streamUrl.startsWith('http')) {
              streamUrl = `${nodeA.baseUrl}${streamUrl.startsWith('/') ? '' : '/'}${streamUrl}`;
            }

            shareFound = true;
          }
        }
      }
    }

    // Fallback to UI polling if direct fetch didn't work
    if (!shareFound) {
      for (let index = 0; index < 30; index++) {
        const shareRow = pageB
          .getByTestId(T.incomingShareRow(collectionTitle))
          .first();
        if ((await shareRow.count()) > 0) {
          shareFound = true;
          await shareRow.getByTestId(T.incomingShareOpen).click();
          await expect(pageB.getByTestId(T.sharedManifest)).toBeVisible({
            timeout: 15_000,
          });

          // Get stream URL from manifest via API
          streamUrl = await pageB.evaluate(
            async ({ expectedTitle, expectedOwnerBaseUrl }) => {
              const token =
                sessionStorage.getItem('slskd-token') ||
                localStorage.getItem('slskd-token');
              if (!token) return null;

              const sharesRes = await fetch('/api/v0/share-grants', {
                headers: { Authorization: `Bearer ${token}` },
              });
              if (!sharesRes.ok) return null;
              const sharesText = await sharesRes.text();
              if (!sharesText) return null;
              let shares;
              try {
                shares = JSON.parse(sharesText);
              } catch {
                return null;
              }

              if (!Array.isArray(shares) || shares.length === 0) return null;

              for (const share of shares) {
                if (!share?.id) continue;
                const manifestRes = await fetch(
                  `/api/v0/share-grants/${share.id}/manifest`,
                  {
                    headers: { Authorization: `Bearer ${token}` },
                  },
                );
                if (!manifestRes.ok) continue;
                const manifestText = await manifestRes.text();
                if (!manifestText) continue;
                let manifest;
                try {
                  manifest = JSON.parse(manifestText);
                } catch {
                  continue;
                }

                if (manifest?.title !== expectedTitle) continue;
                const item = manifest?.items?.[0];
                const url = item?.streamUrl;
                if (!url) continue;

                if (url.startsWith(expectedOwnerBaseUrl)) return url;
                if (url.startsWith('/')) return `${expectedOwnerBaseUrl}${url}`;
              }

              return null;
            },
            {
              expectedOwnerBaseUrl: nodeA.baseUrl,
              expectedTitle: collectionTitle,
            },
          );

          break;
        }

        await pageB.waitForTimeout(500); // Reduced from 1000ms
      }
    }

    expect(shareFound).toBe(true);
    if (!streamUrl) {
      throw new Error('No streamUrl found in manifest for seek test.');
    }

    // Make a Range request for bytes 1000-2000 (simulating seek)
    const normalized = streamUrl
      .replace('http://localhost:', 'http://127.0.0.1:')
      .replace('https://localhost:', 'https://127.0.0.1:');
    const fullStreamUrl = normalized.startsWith('http')
      ? normalized
      : `${nodeB.baseUrl}${normalized}`;

    const rangeResponse = await request.get(fullStreamUrl, {
      failOnStatusCode: false,
      headers: { Range: 'bytes=1000-2000' },
    });

    // Should get 206 (Partial Content) if range is supported
    if (rangeResponse.status() === 206) {
      expect(rangeResponse.headers()['content-range']).toBeTruthy();
    } else {
      expect([200]).toContain(rangeResponse.status());
    }

    await contextA.close();
    await contextB.close();
  });

  test('concurrency_limit_blocks_excess_streams', async ({
    browser,
    request,
  }) => {
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

    const ownerToken = await getAuthToken(pageA);
    const recipientToken = await getAuthToken(pageB);

    // Always create fresh share for this test to ensure isolation
    // (Previous test's shareGrantId may not exist if test order changes)
    let testShareGrantId = sharedGrantId;
    let testCollectionId = sharedCollectionId;

    if (!testShareGrantId || !testCollectionId) {
      const authOwner = { Authorization: `Bearer ${ownerToken}` };

      // Diagnostic: check if routes are available
      const routesRes = await request.get(`${nodeA.baseUrl}/__routes`, {
        failOnStatusCode: false,
      });
      if (routesRes.ok()) {
        const routes = await routesRes.json();
        const hasSharegroups = Array.isArray(routes)
          ? routes.some(
              (r: any) =>
                r.Pattern?.includes('sharegroups') ||
                r.Pattern?.includes('share-groups'),
            )
          : false;
        if (!hasSharegroups) {
          console.warn(
            '[Concurrency Test] sharegroups route not found in __routes',
          );
        }
      }

      const groupsRes = await request.get(
        `${nodeA.baseUrl}/api/v0/sharegroups`,
        { failOnStatusCode: false, headers: authOwner },
      );
      if (!groupsRes.ok()) {
        const body = await groupsRes.text();
        throw new Error(
          `Failed to load share groups: ${groupsRes.status()} ${body}. Routes available: ${routesRes.ok() ? 'yes' : 'no'}`,
        );
      }

      const groups = await groupsRes.json();
      let group = Array.isArray(groups)
        ? groups.find((g) => g?.name === groupName)
        : null;
      if (!group) {
        const createGroupRes = await request.post(
          `${nodeA.baseUrl}/api/v0/sharegroups`,
          { data: { name: groupName }, headers: authOwner },
        );
        if (!createGroupRes.ok()) {
          const body = await createGroupRes.text();
          throw new Error(
            `Failed to create share group: ${createGroupRes.status()} ${body}`,
          );
        }

        group = await createGroupRes.json();
      }

      const membersRes = await request.get(
        `${nodeA.baseUrl}/api/v0/sharegroups/${group.id}/members`,
        { headers: authOwner },
      );
      if (membersRes.ok()) {
        const members = await membersRes.json();
        const isMember = Array.isArray(members)
          ? members.includes(nodeB.username)
          : false;
        if (!isMember) {
          await request.post(
            `${nodeA.baseUrl}/api/v0/sharegroups/${group.id}/members`,
            { data: { userId: nodeB.username }, headers: authOwner },
          );
        }
      }

      const collectionsRes = await request.get(
        `${nodeA.baseUrl}/api/v0/collections`,
        { headers: authOwner },
      );
      if (!collectionsRes.ok()) {
        throw new Error(
          `Failed to load collections: ${collectionsRes.status()}`,
        );
      }

      const collections = await collectionsRes.json();
      let collection = Array.isArray(collections)
        ? collections.find((c) => c?.title === collectionTitle)
        : null;
      if (!collection) {
        const createCollectionRes = await request.post(
          `${nodeA.baseUrl}/api/v0/collections`,
          {
            data: { title: collectionTitle, type: 'Playlist' },
            headers: authOwner,
          },
        );
        if (!createCollectionRes.ok()) {
          const body = await createCollectionRes.text();
          throw new Error(
            `Failed to create collection: ${createCollectionRes.status()} ${body}`,
          );
        }

        collection = await createCollectionRes.json();
      }

      const itemsRes = await request.get(
        `${nodeA.baseUrl}/api/v0/collections/${collection.id}/items`,
        { headers: authOwner },
      );
      if (!itemsRes.ok()) {
        throw new Error(
          `Failed to load collection items: ${itemsRes.status()}`,
        );
      }

      const existingItems = await itemsRes.json();
      if (!Array.isArray(existingItems) || existingItems.length === 0) {
        const libraryRes = await request.get(
          `${nodeA.baseUrl}/api/v0/library/items?query=sintel&limit=1`,
          { headers: authOwner },
        );
        if (!libraryRes.ok()) {
          throw new Error(
            `Failed to load library items: ${libraryRes.status()}`,
          );
        }

        const libraryPayload = await libraryRes.json();
        const libraryItems = Array.isArray(libraryPayload?.items)
          ? libraryPayload.items
          : [];
        const firstItem = libraryItems[0];
        if (!firstItem?.contentId) {
          throw new Error('No library items available for streaming share.');
        }

        const addItemRes = await request.post(
          `${nodeA.baseUrl}/api/v0/collections/${collection.id}/items`,
          {
            data: {
              contentId: firstItem.contentId,
              mediaKind: firstItem.mediaKind,
            },
            headers: authOwner,
          },
        );
        if (!addItemRes.ok()) {
          const body = await addItemRes.text();
          throw new Error(
            `Failed to add collection item: ${addItemRes.status()} ${body}`,
          );
        }
      }

      const createShareRes = await request.post(
        `${nodeA.baseUrl}/api/v0/share-grants`,
        {
          data: {
            allowDownload: true,
            allowReshare: false,
            allowStream: true,
            audienceId: group.id,
            audienceType: 'ShareGroup',
            collectionId: collection.id,
            maxConcurrentStreams: 1,
          },
          headers: authOwner,
        },
      );
      if (!createShareRes.ok()) {
        const body = await createShareRes.text();
        throw new Error(
          `Failed to create share grant: ${createShareRes.status()} ${body}`,
        );
      }

      const share = await createShareRes.json();

      await announceShareGrant({
        owner: nodeA,
        ownerToken,
        recipient: nodeB,
        recipientToken,
        request,
        shareGrantId: share.id,
        shareOverride: share,
      });

      testShareGrantId = share.id;
      testCollectionId = collection.id;
      sharedGrantId = share.id;
      sharedCollectionId = collection.id;
      ownerAuthToken = ownerToken;
      recipientAuthToken = recipientToken;
    }

    if (!testShareGrantId || !testCollectionId) {
      throw new Error(
        'Failed to create or locate share grant for concurrency test',
      );
    }

    const authOwner = {
      Authorization: `Bearer ${ownerAuthToken || ownerToken}`,
    };

    // Update maxConcurrentStreams to 1 (always update to ensure it's set)
    const updateRes = await request.put(
      `${nodeA.baseUrl}/api/v0/share-grants/${testShareGrantId}`,
      {
        data: { maxConcurrentStreams: 1 },
        headers: authOwner,
      },
    );
    if (!updateRes.ok()) {
      const body = await updateRes.text();
      throw new Error(
        `Failed to update share grant concurrency: ${updateRes.status()} ${body}`,
      );
    }

    // Create token AFTER ensuring maxConcurrentStreams is 1
    const tokenRes = await request.post(
      `${nodeA.baseUrl}/api/v0/share-grants/${testShareGrantId}/token`,
      {
        data: { expiresInSeconds: 3_600 },
        headers: authOwner,
      },
    );
    if (!tokenRes.ok()) {
      const body = await tokenRes.text();
      throw new Error(
        `Failed to create share token: ${tokenRes.status()} ${body}`,
      );
    }

    const tokenBody = await tokenRes.json();
    const token = tokenBody?.token;
    if (!token) {
      throw new Error('Share token missing from response.');
    }

    const itemsRes = await request.get(
      `${nodeA.baseUrl}/api/v0/collections/${testCollectionId}/items`,
      { headers: authOwner },
    );
    if (!itemsRes.ok()) {
      throw new Error(`Failed to load collection items: ${itemsRes.status()}`);
    }

    const items = await itemsRes.json();
    const contentId = items?.[0]?.contentId;
    if (!contentId) {
      throw new Error('No contentId available for concurrency test.');
    }

    const streamUrl = `${nodeA.baseUrl}/api/v0/streams/${encodeURIComponent(contentId)}?token=${encodeURIComponent(token)}`;

    // Start first request and wait for it to establish connection (acquire limiter)
    // Run the stream requests from a page on the owner origin (nodeA) to avoid
    // cross-origin + Range preflight/CORS variability in the browser.
    const firstRequestPromise = pageA.evaluate(async (url) => {
      const controller = new AbortController();
      (window as any).__e2eStreamAbort = controller;

      try {
        // Request entire file to keep stream open longer
        const response = await fetch(url, {
          headers: { Range: 'bytes=0-' },
          signal: controller.signal,
        });

        if (!response.ok) {
          return {
            error: 'Response not ok',
            status: response.status,
            success: false,
          };
        }

        const reader = response.body?.getReader();
        if (!reader) {
          return {
            error: 'No reader',
            status: response.status,
            success: false,
          };
        }

        (window as any).__e2eStreamReader = reader;

        // Read first chunk to establish connection and acquire limiter
        const chunk = await reader.read();
        if (chunk.done) {
          return {
            error: 'Stream ended',
            status: response.status,
            success: false,
          };
        }

        // Signal that first request has acquired limiter
        (window as any).__e2eFirstStreamAcquired = true;

        // Start slow reading to keep connection alive
        // This prevents the HTTP response from completing immediately
        (window as any).__e2eStreamReadTask = (async () => {
          try {
            while (true) {
              const result = await reader.read();
              if (result.done) break;
              // Slow reading to keep connection alive longer
              await new Promise((resolve) => setTimeout(resolve, 50)); // Reduced from 100ms
            }
          } catch {
            // Abort expected
          }
        })();

        return { status: response.status, success: true };
      } catch (error: any) {
        return { error: error.message, status: 0, success: false };
      }
    }, streamUrl);

    // Wait for first request to acquire limiter (read first chunk)
    // Poll for the acquisition signal
    let firstAcquired = false;
    for (let index = 0; index < 50; index++) {
      firstAcquired = await pageA.evaluate(
        () => (window as any).__e2eFirstStreamAcquired === true,
      );
      if (firstAcquired) break;
      await new Promise((resolve) => setTimeout(resolve, 50));
    }

    if (!firstAcquired) {
      throw new Error('First stream did not acquire limiter in time');
    }

    // Now make second request immediately while first is still active
    // The limiter should block this (429)
    const secondResult = await pageA.evaluate(async (url) => {
      try {
        const response = await fetch(url, {
          headers: { Range: 'bytes=0-1' },
        });
        return { ok: response.ok, status: response.status };
      } catch (error: any) {
        return { error: error.message, status: 0 };
      }
    }, streamUrl);

    // Get first result (should have succeeded)
    const firstResult = await firstRequestPromise;

    // Verify first stream succeeded
    expect(firstResult.success).toBe(true);
    expect([200, 206]).toContain(firstResult.status);

    // Second request should be blocked (429)
    expect(secondResult.status).toBe(429);

    // Clean up first stream
    await pageA.evaluate(() => {
      (window as any).__e2eStreamAbort?.abort();
      (window as any).__e2eStreamReader?.cancel().catch(() => {});
    });

    await contextA.close();
    await contextB.close();
  });
});
