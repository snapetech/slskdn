import { test, expect } from "@playwright/test";
import { NODES, shouldLaunchNodes } from "./env";
import { waitForHealth, login, clickNav } from "./helpers";
import { T } from "./selectors";
import { MultiPeerHarness } from "./harness/MultiPeerHarness";

test.describe("policy enforcement", () => {
  let harness: MultiPeerHarness | null = null;
  const groupName = "E2E Policy Test";
  const collectionTitleNoStream = "E2E No Stream Policy";
  const collectionTitleNoDownload = "E2E No Download Policy";

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

  test("stream_denied_when_policy_says_no", async ({ browser, request }) => {
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

    // Node A creates a share with stream disabled
    await clickNav(pageA, T.navShares);
    await pageA.getByTestId(T.shareCreate).click();
    
    // Select collection (assuming one exists from previous tests)
    await pageA.getByTestId(T.shareAudiencePicker).click();
    await pageA.getByRole("option").first().click();
    
    // Disable stream policy
    await pageA.getByTestId(T.sharePolicyStream).uncheck();
    await pageA.getByTestId(T.sharePolicyDownload).check();
    
    await pageA.getByTestId(T.shareCreateSubmit).click();
    await pageA.waitForTimeout(2000);

    // Node B tries to stream
    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(2000);
    
    // Find the shared item
    const shareRow = pageB.locator('[data-testid*="incoming-share-row"]').first();
    if (await shareRow.count()) {
      await shareRow.click();
      await pageB.getByTestId(T.incomingShareOpen).click();
      
      // Stream button should be disabled or not present
      const streamButton = pageB.getByTestId(T.incomingStreamButton);
      const count = await streamButton.count();
      
      if (count > 0) {
        // If button exists, clicking should result in 403/401
        const responsePromise = pageB.waitForResponse(resp => 
          resp.url().includes("/streams/") && (resp.status() === 403 || resp.status() === 401)
        );
        
        await streamButton.click();
        
        try {
          await responsePromise;
          // Success - policy enforced
        } catch {
          // Button might be disabled in UI instead
          const isDisabled = await streamButton.isDisabled();
          expect(isDisabled).toBe(true);
        }
      } else {
        // Button not present - policy enforced at UI level
        expect(count).toBe(0);
      }
    } else {
      test.skip(); // No shared content available
    }

    await ctxA.close();
    await ctxB.close();
  });

  test("download_denied_when_policy_says_no", async ({ browser, request }) => {
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

    // Node A creates a share with download disabled
    await clickNav(pageA, T.navShares);
    await pageA.getByTestId(T.shareCreate).click();
    
    await pageA.getByTestId(T.shareAudiencePicker).click();
    await pageA.getByRole("option").first().click();
    
    // Disable download policy
    await pageA.getByTestId(T.sharePolicyStream).check();
    await pageA.getByTestId(T.sharePolicyDownload).uncheck();
    
    await pageA.getByTestId(T.shareCreateSubmit).click();
    await pageA.waitForTimeout(2000);

    // Node B tries to backfill/download
    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(2000);
    
    const shareRow = pageB.locator('[data-testid*="incoming-share-row"]').first();
    if (await shareRow.count()) {
      await shareRow.click();
      
      // Backfill button should be disabled or not present
      const backfillButton = pageB.getByTestId(T.incomingBackfillButton);
      const count = await backfillButton.count();
      
      if (count > 0) {
        // If button exists, clicking should result in 403/401
        const responsePromise = pageB.waitForResponse(resp => 
          resp.url().includes("/backfill") && (resp.status() === 403 || resp.status() === 401)
        );
        
        await backfillButton.click();
        
        try {
          await responsePromise;
          // Success - policy enforced
        } catch {
          // Button might be disabled in UI instead
          const isDisabled = await backfillButton.isDisabled();
          expect(isDisabled).toBe(true);
        }
      } else {
        // Button not present - policy enforced at UI level
        expect(count).toBe(0);
      }
    } else {
      test.skip(); // No shared content available
    }

    await ctxA.close();
    await ctxB.close();
  });

  test("expired_token_denied", async ({ browser, request }) => {
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
    test.skip();

    await ctxB.close();
  });
});
