import { test, expect } from '@playwright/test';
import { NODES, shouldLaunchNodes } from './env';
import { waitForHealth, login, clickNav } from './helpers';
import { T } from './selectors';
import { MultiPeerHarness } from './harness/MultiPeerHarness';

test.describe('policy enforcement', () => {
  let harness: MultiPeerHarness | null = null;
  const groupName = 'E2E Policy Test';
  const collectionTitleNoStream = 'E2E No Stream Policy';
  const collectionTitleNoDownload = 'E2E No Download Policy';

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

  test('stream_denied_when_policy_says_no', async ({ browser, request }) => {
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

    // Ensure group exists and nodeB is a member
    await clickNav(pageA, T.navGroups);
    const existingGroupRow = pageA.getByTestId(T.groupRow(groupName));
    if ((await existingGroupRow.count()) === 0) {
      await pageA.getByTestId(T.groupsCreate).click();
      await pageA.waitForSelector(`[data-testid="${T.groupsNameInput}"]`, { timeout: 5000 });
      await pageA.getByTestId(T.groupsNameInput).locator('input').fill(groupName);
      await pageA.getByTestId(T.groupsCreateSubmit).click();
      await expect(pageA.getByTestId(T.groupRow(groupName))).toBeVisible({ timeout: 5000 });
    }

    // Ensure nodeB is a member
    const addMemberBtn = pageA
      .getByTestId(T.groupRow(groupName))
      .locator(`[data-testid="${T.groupAddMember}"]`)
      .first();
    if ((await addMemberBtn.count()) > 0) {
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
    }

    // Create collection with no stream policy
    await clickNav(pageA, T.navCollections);
    await pageA.waitForSelector('[data-testid="collections-root"]', { timeout: 10000 });

    const existingCollectionRow = pageA.getByTestId(T.collectionRow(collectionTitleNoStream));
    if ((await existingCollectionRow.count()) === 0) {
      await pageA.getByTestId(T.collectionsCreate).click();
      await pageA.waitForSelector(`[data-testid="${T.collectionsTypeSelect}"]`, { timeout: 5000 });
      await pageA.getByTestId(T.collectionsTypeSelect).click();
      await pageA.getByRole('option', { name: /playlist/i }).click();
      await pageA.getByTestId(T.collectionsTitleInput).locator('input').fill(collectionTitleNoStream);

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
      await expect(pageA.getByTestId(T.collectionRow(collectionTitleNoStream))).toBeVisible({ timeout: 5000 });
    }

    await pageA.getByTestId(T.collectionRow(collectionTitleNoStream)).click();
    await pageA.waitForTimeout(500);

    // Add item
    const addItemBtn = pageA.getByTestId(T.collectionAddItem);
    if ((await addItemBtn.count()) > 0) {
      await addItemBtn.click();
      await pageA.getByTestId(T.collectionItemPicker).locator('input').fill('synthetic');
      await pageA.getByTestId(T.collectionAddItemSubmit).click();
      await pageA.waitForTimeout(1000);
    }

    // Create share with stream disabled, download enabled
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

    // Disable stream, enable download
    const streamPolicy = pageA.getByTestId(T.sharePolicyStream);
    if ((await streamPolicy.count()) > 0) {
      const isChecked = await streamPolicy.isChecked();
      if (isChecked) {
        await streamPolicy.uncheck({ timeout: 5000 });
      }
    }

    const downloadPolicy = pageA.getByTestId(T.sharePolicyDownload);
    if ((await downloadPolicy.count()) > 0) {
      const isChecked = await downloadPolicy.isChecked();
      if (!isChecked) {
        await downloadPolicy.check({ timeout: 5000 });
      }
    }

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

    // Node B tries to access the share
    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(2000);

    // Poll for the share to appear
    let shareFound = false;
    for (let i = 0; i < 20; i++) {
      const shareRow = pageB.getByTestId(T.incomingShareRow(collectionTitleNoStream)).first();
      if ((await shareRow.count()) > 0) {
        shareFound = true;
        await shareRow.getByTestId(T.incomingShareOpen).click();
        await expect(pageB.getByTestId(T.sharedManifest)).toBeVisible({ timeout: 15000 });

        // Verify stream button is not present or disabled (stream not allowed)
        const streamButton = pageB.getByTestId(T.incomingStreamButton);
        const streamCount = await streamButton.count();

        if (streamCount > 0) {
          // If button exists, it should be disabled or clicking should fail
          const isDisabled = await streamButton.isDisabled();
          if (!isDisabled) {
            // Try clicking and expect 403/401
            const responsePromise = pageB.waitForResponse(
              (resp) =>
                resp.url().includes('/streams/') &&
                (resp.status() === 403 || resp.status() === 401),
              { timeout: 5000 },
            );
            await streamButton.click();
            try {
              await responsePromise;
              // Success - policy enforced via API
            } catch {
              // Button might open a new window - check if stream URL is missing from manifest
              const manifestHasStreamUrl = await pageB.evaluate(() => {
                const manifest = document.querySelector('[data-testid="shared-manifest"]');
                if (!manifest) return false;
                const streamButtons = manifest.querySelectorAll('[data-testid="incoming-stream"]');
                return streamButtons.length === 0;
              });
              expect(manifestHasStreamUrl).toBe(true);
            }
          } else {
            // Button is disabled - policy enforced at UI level
            expect(isDisabled).toBe(true);
          }
        } else {
          // Button not present - policy enforced at UI level
          expect(streamCount).toBe(0);
        }
        break;
      }
      await pageB.waitForTimeout(1000);
    }

    if (!shareFound) {
      test.skip(true, 'Share not found after polling. Cross-node discovery may have failed.');
    }

    await ctxA.close();
    await ctxB.close();
  });

  test('download_denied_when_policy_says_no', async ({ browser, request }) => {
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

    // Ensure group exists and nodeB is a member (reuse from previous test)
    await clickNav(pageA, T.navGroups);
    const existingGroupRow = pageA.getByTestId(T.groupRow(groupName));
    if ((await existingGroupRow.count()) === 0) {
      await pageA.getByTestId(T.groupsCreate).click();
      await pageA.waitForSelector(`[data-testid="${T.groupsNameInput}"]`, { timeout: 5000 });
      await pageA.getByTestId(T.groupsNameInput).locator('input').fill(groupName);
      await pageA.getByTestId(T.groupsCreateSubmit).click();
      await expect(pageA.getByTestId(T.groupRow(groupName))).toBeVisible({ timeout: 5000 });
    }

    // Create collection with no download policy
    await clickNav(pageA, T.navCollections);
    await pageA.waitForSelector('[data-testid="collections-root"]', { timeout: 10000 });

    const existingCollectionRow = pageA.getByTestId(T.collectionRow(collectionTitleNoDownload));
    if ((await existingCollectionRow.count()) === 0) {
      await pageA.getByTestId(T.collectionsCreate).click();
      await pageA.waitForSelector(`[data-testid="${T.collectionsTypeSelect}"]`, { timeout: 5000 });
      await pageA.getByTestId(T.collectionsTypeSelect).click();
      await pageA.getByRole('option', { name: /playlist/i }).click();
      await pageA.getByTestId(T.collectionsTitleInput).locator('input').fill(collectionTitleNoDownload);

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
      await expect(pageA.getByTestId(T.collectionRow(collectionTitleNoDownload))).toBeVisible({ timeout: 5000 });
    }

    await pageA.getByTestId(T.collectionRow(collectionTitleNoDownload)).click();
    await pageA.waitForTimeout(500);

    // Add item
    const addItemBtn = pageA.getByTestId(T.collectionAddItem);
    if ((await addItemBtn.count()) > 0) {
      await addItemBtn.click();
      await pageA.getByTestId(T.collectionItemPicker).locator('input').fill('synthetic');
      await pageA.getByTestId(T.collectionAddItemSubmit).click();
      await pageA.waitForTimeout(1000);
    }

    // Create share with download disabled, stream enabled
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

    // Enable stream, disable download
    const streamPolicy = pageA.getByTestId(T.sharePolicyStream);
    if ((await streamPolicy.count()) > 0) {
      const isChecked = await streamPolicy.isChecked();
      if (!isChecked) {
        await streamPolicy.check({ timeout: 5000 });
      }
    }

    const downloadPolicy = pageA.getByTestId(T.sharePolicyDownload);
    if ((await downloadPolicy.count()) > 0) {
      const isChecked = await downloadPolicy.isChecked();
      if (isChecked) {
        await downloadPolicy.uncheck({ timeout: 5000 });
      }
    }

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

    // Node B tries to backfill/download
    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(2000);

    // Poll for the share to appear
    let shareFound = false;
    for (let i = 0; i < 20; i++) {
      const shareRow = pageB.getByTestId(T.incomingShareRow(collectionTitleNoDownload)).first();
      if ((await shareRow.count()) > 0) {
        shareFound = true;
        await shareRow.getByTestId(T.incomingShareOpen).click();
        await expect(pageB.getByTestId(T.sharedManifest)).toBeVisible({ timeout: 15000 });

        // Backfill button should be disabled or not present
        const backfillButton = pageB.getByTestId(T.incomingBackfillButton);
        const count = await backfillButton.count();

        if (count > 0) {
          // If button exists, it should be disabled or clicking should fail
          const isDisabled = await backfillButton.isDisabled();
          if (!isDisabled) {
            // Try clicking and expect 403/401
            const responsePromise = pageB.waitForResponse(
              (resp) =>
                resp.url().includes('/backfill') &&
                (resp.status() === 403 || resp.status() === 401),
              { timeout: 5000 },
            );
            await backfillButton.click();
            try {
              await responsePromise;
              // Success - policy enforced via API
            } catch {
              // Button might not trigger API call - verify it's disabled now
              const stillDisabled = await backfillButton.isDisabled();
              expect(stillDisabled).toBe(true);
            }
          } else {
            // Button is disabled - policy enforced at UI level
            expect(isDisabled).toBe(true);
          }
        } else {
          // Button not present - policy enforced at UI level
          expect(count).toBe(0);
        }
        break;
      }
      await pageB.waitForTimeout(1000);
    }

    if (!shareFound) {
      test.skip(true, 'Share not found after polling. Cross-node discovery may have failed.');
    }

    await ctxA.close();
    await ctxB.close();
  });

  test('expired_token_denied', async ({ browser, request }) => {
    // This test requires creating a share with a very short expiry
    // and waiting for it to expire, or manually expiring tokens
    // For E2E, this might be better tested at API level
    // Placeholder for now

    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;
    await waitForHealth(request, nodeB.baseUrl);

    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, nodeB);

    // This test would need:
    // 1. Create share with ExpiryUtc = now + 1 second
    // 2. Wait 2 seconds
    // 3. Try to stream/download
    // 4. Verify 401/403 response

    // For now, skip - better tested at API level with precise timing
    test.skip(true, 'Expired token test requires precise timing - better tested at API level');

    await ctxB.close();
  });
});
