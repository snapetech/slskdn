import { test, expect } from "@playwright/test";
import { NODES } from "./env";
import { waitForHealth, login, clickNav } from "./helpers";
import { T } from "./selectors";

test.describe.configure({ mode: "serial" });

test.describe("multi-peer sharing", () => {
  const groupName = "E2E Crew";
  const collectionTitle = "E2E Playlist";

  test("invite_add_friend", async ({ browser, request }) => {
    await waitForHealth(request, NODES.A.baseUrl);
    await waitForHealth(request, NODES.B.baseUrl);

    const ctxA = await browser.newContext();
    const ctxB = await browser.newContext();
    const pageA = await ctxA.newPage();
    const pageB = await ctxB.newPage();

    await login(pageA, NODES.A);
    await login(pageB, NODES.B);

    await clickNav(pageA, T.navContacts);
    await pageA.getByTestId(T.contactsCreateInvite).click();
    
    // Wait for invite modal and get invite link
    await pageA.waitForSelector(`[data-testid="${T.contactsInviteOutput}"]`, { timeout: 10000 });
    const invite = await pageA.getByTestId(T.contactsInviteOutput).inputValue();

    expect(invite.length).toBeGreaterThan(20);

    await clickNav(pageB, T.navContacts);
    await pageB.getByTestId(T.contactsAddFriend).click();
    
    // Wait for add friend modal
    await pageB.waitForSelector(`[data-testid="${T.contactsAddInviteInput}"]`, { timeout: 5000 });
    await pageB.getByTestId(T.contactsAddInviteInput).fill(invite);
    await pageB.getByTestId(T.contactsContactNickname).fill("nodeA");
    await pageB.getByTestId(T.contactsAddInviteSubmit).click();

    // Contact row should appear (use a stable label; simplest is displayName)
    // Wait a bit for the contact to appear after adding
    await pageB.waitForTimeout(2000);
    await expect(pageB.getByTestId(T.contactsRow("nodeA"))).toBeVisible({ timeout: 10000 });

    await ctxA.close();
    await ctxB.close();
  });

  test("create_group_add_member", async ({ browser, request }) => {
    await waitForHealth(request, NODES.A.baseUrl);

    const ctxA = await browser.newContext();
    const pageA = await ctxA.newPage();
    await login(pageA, NODES.A);

    await clickNav(pageA, T.navGroups);
    await pageA.getByTestId(T.groupsCreate).click();
    
    // Wait for create group modal
    await pageA.waitForSelector(`[data-testid="${T.groupsNameInput}"]`, { timeout: 5000 });
    await pageA.getByTestId(T.groupsNameInput).fill(groupName);
    await pageA.getByTestId(T.groupsCreateSubmit).click();

    await expect(pageA.getByTestId(T.groupRow(groupName))).toBeVisible({ timeout: 10000 });

    // Click on the group row to view details (or use a different approach based on UI)
    await pageA.getByTestId(T.groupRow(groupName)).click();
    
    // Add member
    await pageA.getByTestId(T.groupAddMember).click();
    
    // Wait for add member modal
    await pageA.waitForSelector(`[data-testid="${T.groupMemberPicker}"]`, { timeout: 5000 });
    await pageA.getByTestId(T.groupMemberPicker).click();
    
    // Select nodeB from contacts (adjust selector based on actual dropdown behavior)
    await pageA.getByRole("option", { name: /nodeB/i }).click();
    await pageA.getByTestId(T.groupMemberAddSubmit).click();

    // Verify member was added (adjust selector based on actual UI)
    await expect(pageA.getByTestId(T.groupMembers)).toContainText(/nodeB/i, { timeout: 10000 });

    await ctxA.close();
  });

  test("create_collection_share_to_group", async ({ browser, request }) => {
    await waitForHealth(request, NODES.A.baseUrl);

    const ctxA = await browser.newContext();
    const pageA = await ctxA.newPage();
    await login(pageA, NODES.A);

    // Navigate to collections (if you have a collections page)
    // For now, this test is a placeholder - adjust based on your actual collections UI
    await clickNav(pageA, T.navCollections);
    
    // Create collection
    await pageA.getByTestId(T.collectionsCreate).click();
    
    await pageA.waitForSelector(`[data-testid="${T.collectionsTypeSelect}"]`, { timeout: 5000 });
    await pageA.getByTestId(T.collectionsTypeSelect).click();
    await pageA.getByRole("option", { name: /playlist/i }).click();

    await pageA.getByTestId(T.collectionsTitleInput).fill(collectionTitle);
    await pageA.getByTestId(T.collectionsCreateSubmit).click();

    await expect(pageA.getByTestId(T.collectionRow(collectionTitle))).toBeVisible({ timeout: 10000 });
    await pageA.getByTestId(T.collectionRow(collectionTitle)).click();

    // Add a couple fixture items (your UI will vary; keep picker stable)
    await pageA.getByTestId(T.collectionAddItem).click();
    await pageA.getByTestId(T.collectionItemPicker).fill("synthetic");
    await pageA.getByRole("option").first().click();
    await pageA.getByTestId(T.collectionAddItemSubmit).click();

    await expect(pageA.getByTestId(T.collectionItems)).toContainText(/synthetic/i, { timeout: 10000 });

    // Share it
    await clickNav(pageA, T.navShares);
    await pageA.getByTestId(T.shareCreate).click();
    await pageA.getByTestId(T.shareAudiencePicker).click();
    await pageA.getByRole("option", { name: new RegExp(groupName, "i") }).click();

    await pageA.getByTestId(T.sharePolicyStream).check();
    await pageA.getByTestId(T.sharePolicyDownload).check();

    await pageA.getByTestId(T.shareCreateSubmit).click();
    await expect(pageA.getByTestId(T.sharesList)).toContainText(collectionTitle, { timeout: 10000 });

    await ctxA.close();
  });

  test("recipient_sees_shared_manifest", async ({ browser, request }) => {
    await waitForHealth(request, NODES.B.baseUrl);

    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, NODES.B);

    await clickNav(pageB, T.navSharedWithMe);
    await expect(pageB.getByTestId(T.incomingShareRow(collectionTitle))).toBeVisible({ timeout: 10000 });

    await pageB.getByTestId(T.incomingShareRow(collectionTitle)).click();
    await pageB.getByTestId(T.incomingShareOpen).click();

    await expect(pageB.getByTestId(T.sharedManifest)).toContainText(collectionTitle, { timeout: 10000 });

    await ctxB.close();
  });

  test("stream_and_backfill", async ({ browser, request }) => {
    await waitForHealth(request, NODES.B.baseUrl);

    const ctxB = await browser.newContext();
    const pageB = await ctxB.newPage();
    await login(pageB, NODES.B);

    // Watch for a stream response; confirm 206 (Range)
    const streamResponse = pageB.waitForResponse(resp => {
      return resp.url().includes("/api/") && resp.url().includes("/streams/") && resp.status() === 206;
    });

    await clickNav(pageB, T.navSharedWithMe);
    await pageB.getByTestId(T.incomingShareRow(collectionTitle)).click();
    await pageB.getByTestId(T.incomingStreamButton).click();

    await streamResponse;

    // Backfill download
    await pageB.getByTestId(T.incomingBackfillButton).click();

    // Confirm at least one download shows up and completes.
    // Adjust these testids to your downloads UI.
    await clickNav(pageB, T.navDownloads);
    const row = pageB.getByTestId("download-row-first");
    await expect(row).toBeVisible({ timeout: 10000 });
    await expect(row).toContainText(/completed|seeding|finished/i, { timeout: 30000 });

    await ctxB.close();
  });
});
