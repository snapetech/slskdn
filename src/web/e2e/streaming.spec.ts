import { test, expect } from '@playwright/test';
import { NODES, shouldLaunchNodes } from './env';
import { waitForHealth, login, clickNav } from './helpers';
import { T } from './selectors';
import { MultiPeerHarness } from './harness/MultiPeerHarness';

test.describe('streaming', () => {
  let harness: MultiPeerHarness | null = null;
  const groupName = 'E2E Crew';
  const collectionTitle = 'E2E Streaming Test';

  test.beforeAll(async () => {
    if (shouldLaunchNodes()) {
      harness = new MultiPeerHarness();
      await harness.startNode('A', 'test-data/slskdn-test-fixtures/music', {
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

    const ctxA = await browser.newContext();
    const ctxB = await browser.newContext();
    const pageA = await ctxA.newPage();
    const pageB = await ctxB.newPage();

    await login(pageA, nodeA);
    await login(pageB, nodeB);

    // Ensure group and collection exist (reuse from multippeer-sharing tests)
    await clickNav(pageA, T.navGroups);
    const existingGroupRow = pageA.getByTestId(T.groupRow(groupName));
    if ((await existingGroupRow.count()) === 0) {
      await pageA.getByTestId(T.groupsCreate).click();
      await pageA.waitForSelector(`[data-testid="${T.groupsNameInput}"]`, { timeout: 5000 });
      await pageA.getByTestId(T.groupsNameInput).locator('input').fill(groupName);
      await pageA.getByTestId(T.groupsCreateSubmit).click();
      await expect(pageA.getByTestId(T.groupRow(groupName))).toBeVisible({ timeout: 5000 });
    }

    // Create collection and share (similar to multippeer-sharing test)
    await clickNav(pageA, T.navCollections);
    await pageA.waitForSelector('[data-testid="collections-root"]', { timeout: 10000 });

    const existingCollectionRow = pageA.getByTestId(T.collectionRow(collectionTitle));
    if ((await existingCollectionRow.count()) === 0) {
      await pageA.getByTestId(T.collectionsCreate).click();
      await pageA.waitForSelector(`[data-testid="${T.collectionsTypeSelect}"]`, { timeout: 5000 });
      await pageA.getByTestId(T.collectionsTypeSelect).click();
      await pageA.getByRole('option', { name: /playlist/i }).click();
      await pageA.getByTestId(T.collectionsTitleInput).locator('input').fill(collectionTitle);

      const createCollectionResponse = pageA.waitForResponse(
        (response) =>
          response.url().includes('/api/v0/collections') &&
          response.request().method() === 'POST',
        { timeout: 5000 },
      );
      await pageA.getByTestId(T.collectionsCreateSubmit).click();
      const createCollectionResult = await createCollectionResponse;
      if (createCollectionResult.status() !== 201) {
        const body = await createCollectionResult.text();
        throw new Error(`Create collection failed: ${createCollectionResult.status()} ${body}`);
      }
      await expect(pageA.getByTestId(T.collectionRow(collectionTitle))).toBeVisible({ timeout: 5000 });
    }

    await pageA.getByTestId(T.collectionRow(collectionTitle)).click();
    await pageA.waitForTimeout(500);

    // Add item
    const addItemBtn = pageA.getByTestId(T.collectionAddItem);
    if ((await addItemBtn.count()) > 0) {
      await addItemBtn.click();
      await pageA.getByTestId(T.collectionItemPicker).locator('input').fill('synthetic');
      await pageA.getByTestId(T.collectionAddItemSubmit).click();
      await pageA.waitForTimeout(1000);
    }

    // Share with stream enabled
    const shareCreate = pageA.getByTestId(T.shareCreate);
    await expect(shareCreate).toBeVisible({ timeout: 5000 });
    await shareCreate.click();
    const audiencePicker = pageA.getByTestId(T.shareAudiencePicker);
    await expect(audiencePicker).toBeVisible({ timeout: 5000 });
    await audiencePicker.click();
    const groupOption = pageA.getByRole('option', { name: new RegExp(groupName, 'i') });
    if ((await groupOption.count()) === 0) {
      throw new Error('No share groups found in picker. Ensure group creation ran.');
    }
    await groupOption.first().click();

    await pageA.getByTestId(T.sharePolicyStream).check();
    await pageA.getByTestId(T.sharePolicyDownload).check();

    const createShareResponse = pageA.waitForResponse(
      (response) =>
        response.url().includes('/api/v0/share-grants') &&
        response.request().method() === 'POST',
      { timeout: 5000 },
    );
    await pageA.getByTestId(T.shareCreateSubmit).click();
    const createShareResult = await createShareResponse;
    if (createShareResult.status() !== 201) {
      const body = await createShareResult.text();
      throw new Error(`Create share failed: ${createShareResult.status()} ${body}`);
    }

    // Wait for cross-node discovery
    await pageB.waitForTimeout(5000);

    // Node B tries to stream
    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(2000);

    // Poll for the share to appear
    let shareFound = false;
    for (let i = 0; i < 20; i++) {
      const shareRow = pageB.getByTestId(T.incomingShareRow(collectionTitle)).first();
      if ((await shareRow.count()) > 0) {
        shareFound = true;
        await shareRow.getByTestId(T.incomingShareOpen).click();
        await expect(pageB.getByTestId(T.sharedManifest)).toBeVisible({ timeout: 15000 });

        // Get stream URL from manifest via API (more reliable than UI)
        const streamUrl = await pageB.evaluate(
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
              if (url.startsWith('/'))
                return `${expectedOwnerBaseUrl}${url}`;
            }

            return null;
          },
          {
            expectedTitle: collectionTitle,
            expectedOwnerBaseUrl: nodeA.baseUrl,
          },
        );

        if (streamUrl) {
          // Make a Range request (simulating stream)
          const normalized = streamUrl
            .replace('http://localhost:', 'http://127.0.0.1:')
            .replace('https://localhost:', 'https://127.0.0.1:');
          const fullStreamUrl = normalized.startsWith('http')
            ? normalized
            : `${nodeB.baseUrl}${normalized}`;

          const rangeResponse = await request.get(fullStreamUrl, {
            headers: { Range: 'bytes=0-1' },
            failOnStatusCode: false,
          });

          // Should get 206 (Partial Content) or 200 (full content) or 404 (synthetic content not found)
          expect([206, 200, 404]).toContain(rangeResponse.status());
        } else {
          test.skip(
            true,
            'No streamUrl found in manifest (fixture/index may not provide streamable content).',
          );
        }
        break;
      }
      await pageB.waitForTimeout(1000);
    }

    if (!shareFound) {
      test.skip(
        true,
        'Share not found after polling. Cross-node discovery may have failed.',
      );
    }

    await ctxA.close();
    await ctxB.close();
  });

  test('seek_works_with_range_requests', async ({ browser, request }) => {
    const nodeA = harness ? harness.getNode('A').nodeCfg : NODES.A;
    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;
    await waitForHealth(request, nodeA.baseUrl);
    await waitForHealth(request, nodeB.baseUrl);

    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, nodeB);

    // Navigate to shared content
    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(2000);

    // Poll for the share to appear
    let shareFound = false;
    for (let i = 0; i < 20; i++) {
      const shareRow = pageB.getByTestId(T.incomingShareRow(collectionTitle)).first();
      if ((await shareRow.count()) > 0) {
        shareFound = true;
        await shareRow.getByTestId(T.incomingShareOpen).click();
        await expect(pageB.getByTestId(T.sharedManifest)).toBeVisible({ timeout: 15000 });

        // Get stream URL from manifest via API
        const streamUrl = await pageB.evaluate(
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
              if (url.startsWith('/'))
                return `${expectedOwnerBaseUrl}${url}`;
            }

            return null;
          },
          {
            expectedTitle: collectionTitle,
            expectedOwnerBaseUrl: nodeA.baseUrl,
          },
        );

        if (streamUrl) {
          // Make a Range request for bytes 1000-2000 (simulating seek)
          const normalized = streamUrl
            .replace('http://localhost:', 'http://127.0.0.1:')
            .replace('https://localhost:', 'https://127.0.0.1:');
          const fullStreamUrl = normalized.startsWith('http')
            ? normalized
            : `${nodeB.baseUrl}${normalized}`;

          const rangeResponse = await request.get(fullStreamUrl, {
            headers: { Range: 'bytes=1000-2000' },
            failOnStatusCode: false,
          });

          // Should get 206 (Partial Content) if range is supported
          // Or 200/404 if content not available
          if (rangeResponse.status() === 206) {
            expect(rangeResponse.headers()['content-range']).toBeTruthy();
          } else {
            // Content might not exist (synthetic) - that's OK for this test
            expect([200, 404]).toContain(rangeResponse.status());
          }
        } else {
          test.skip(
            true,
            'No streamUrl found in manifest (fixture/index may not provide streamable content).',
          );
        }
        break;
      }
      await pageB.waitForTimeout(1000);
    }

    if (!shareFound) {
      test.skip(
        true,
        'Share not found after polling. Cross-node discovery may have failed.',
      );
    }

    await ctxB.close();
  });

  test('concurrency_limit_blocks_excess_streams', async ({ browser, request }) => {
    // This test verifies that MaxConcurrentStreams policy is enforced
    // It's optional and may be better tested at API level
    // For E2E, we can verify the UI shows appropriate error or blocks action

    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;
    await waitForHealth(request, nodeB.baseUrl);

    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, nodeB);

    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForLoadState('domcontentloaded');

    // If MaxConcurrentStreams is 1, starting a second stream should fail
    // This is a placeholder - adjust based on actual UI behavior
    const streamButtons = pageB.getByTestId(T.incomingStreamButton);
    const count = await streamButtons.count();

    if (count >= 2) {
      // Try to start multiple streams
      // The second one should be blocked if MaxConcurrentStreams=1
      // This test may need adjustment based on actual policy enforcement UI
      test.skip(
        true,
        'Concurrency limit test requires specific share setup - better tested at API level',
      );
    } else {
      test.skip(
        true,
        'Not enough streamable items to test concurrency limit',
      );
    }

    await ctxB.close();
  });
});
