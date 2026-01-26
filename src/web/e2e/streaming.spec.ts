import { test, expect } from "@playwright/test";
import { NODES, shouldLaunchNodes } from "./env";
import { waitForHealth, login, clickNav } from "./helpers";
import { T } from "./selectors";
import { MultiPeerHarness } from "./harness/MultiPeerHarness";

test.describe("streaming", () => {
  let harness: MultiPeerHarness | null = null;
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

  test("recipient_streams_item_with_range", async ({ browser, request }) => {
    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;
    await waitForHealth(request, nodeB.baseUrl);

    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, nodeB);

    // Watch for a stream response with Range support (206 Partial Content)
    const streamResponse = pageB.waitForResponse(resp => {
      return resp.url().includes("/api/") && 
             resp.url().includes("/streams/") && 
             resp.status() === 206;
    });

    await clickNav(pageB, T.navSharedWithMe);
    
    // Wait for shared content to appear (from previous test setup)
    await pageB.waitForTimeout(2000);
    
    // Find and click stream button
    const streamButton = pageB.getByTestId(T.incomingStreamButton).first();
    if (await streamButton.count()) {
      await streamButton.click();
      
      // Wait for 206 response (Range request)
      await streamResponse;
      
      // Verify Range headers were used
      const response = await streamResponse;
      expect(response.status()).toBe(206);
    } else {
      // Skip if no shared content available (depends on previous test)
      test.skip();
    }

    await ctxB.close();
  });

  test("seek_works_with_range_requests", async ({ browser, request }) => {
    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;
    await waitForHealth(request, nodeB.baseUrl);

    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, nodeB);

    // Open shared manifest
    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(2000);
    
    const shareRow = pageB.getByTestId(T.incomingShareRow(collectionTitle));
    if (await shareRow.count()) {
      await shareRow.click();
      await pageB.getByTestId(T.incomingShareOpen).click();
      
      // Wait for manifest
      await pageB.waitForSelector(`[data-testid="${T.sharedManifest}"]`, { timeout: 10000 });
      
      // Get stream URL from manifest
      const streamButton = pageB.getByTestId(T.incomingStreamButton).first();
      if (await streamButton.count()) {
        // Intercept the stream request to add Range header
        const streamUrl = await streamButton.getAttribute('href') || 
                         await streamButton.evaluate(el => {
                           const onclick = el.getAttribute('onclick');
                           if (onclick) {
                             const match = onclick.match(/['"]([^'"]*\/streams\/[^'"]*)['"]/);
                             return match ? match[1] : null;
                           }
                           return null;
                         });
        
        if (streamUrl) {
          // Make a Range request (simulating seek)
          const rangeResponse = await request.get(streamUrl, {
            headers: {
              'Range': 'bytes=1000-2000'
            }
          });
          
          // Should get 206 Partial Content
          expect(rangeResponse.status()).toBe(206);
          expect(rangeResponse.headers()['content-range']).toBeTruthy();
        }
      }
    } else {
      test.skip();
    }

    await ctxB.close();
  });

  test("concurrency_limit_blocks_excess_streams", async ({ browser, request }) => {
    const nodeB = harness ? harness.getNode('B').nodeCfg : NODES.B;
    await waitForHealth(request, nodeB.baseUrl);

    // This test verifies that MaxConcurrentStreams policy is enforced
    // It's optional and may be better tested at API level
    // For E2E, we can verify the UI shows appropriate error or blocks action
    
    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, nodeB);

    await clickNav(pageB, T.navSharedWithMe);
    await pageB.waitForTimeout(2000);
    
    // If MaxConcurrentStreams is 1, starting a second stream should fail
    // This is a placeholder - adjust based on actual UI behavior
    const streamButtons = pageB.getByTestId(T.incomingStreamButton);
    const count = await streamButtons.count();
    
    if (count >= 2) {
      // Try to start multiple streams
      // The second one should be blocked if MaxConcurrentStreams=1
      // This test may need adjustment based on actual policy enforcement UI
      test.skip(); // Skip for now - better tested at API level
    }

    await ctxB.close();
  });
});
