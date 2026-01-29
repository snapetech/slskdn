import { test, expect } from '@playwright/test';
import { MultiPeerHarness } from '../harness/MultiPeerHarness';
import { login, getInviteLink } from '../fixtures/helpers';
import { selectors } from '../fixtures/selectors';

/**
 * Sharing tests: Contacts, groups, collections, sharing flows.
 * 
 * These tests verify the complete sharing journey:
 * - Create invite
 * - Add contact from invite
 * - Create share group
 * - Create collection
 * - Share collection to group
 * - Recipient sees shared content
 */
test.describe('Sharing', () => {
  let harness: MultiPeerHarness;

  test.beforeAll(async () => {
    harness = new MultiPeerHarness();
  });

  test.beforeEach(async () => {
    await harness.stopAll();
  });

  test.afterAll(async () => {
    await harness.stopAll();
  });

  test('should create invite and add contact', async ({ page, context }) => {
    const alice = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music');
    const bob = await harness.startNode('bob', 'test-data/slskdn-test-fixtures/book');

    // Alice creates invite
    await login(page, alice.apiUrl, 'admin', 'admin');

    await page.goto(`${alice.apiUrl}/contacts`);
    await page.waitForSelector(selectors.contacts.createInvite, { timeout: 10000 });
    
    await page.click(selectors.contacts.createInvite);
    await page.waitForSelector(selectors.contacts.inviteLink, { timeout: 10000 });
    
    const inviteLink = await getInviteLink(page);
    expect(inviteLink).toContain('slskdn://invite/');
    
    // Get friend code if available
    const friendCode = await page.textContent(selectors.contacts.inviteFriendCode).catch(() => null);
    expect(friendCode).toBeTruthy();

    // Bob adds Alice from invite
    const bobPage = await context.newPage();
    await login(bobPage, bob.apiUrl, 'admin', 'admin');

    await bobPage.goto(`${bob.apiUrl}/contacts`);
    await bobPage.waitForSelector(selectors.contacts.addFriend, { timeout: 10000 });
    
    await bobPage.click(selectors.contacts.addFriend);
    await bobPage.fill(selectors.contacts.inviteLinkInput, inviteLink);
    await bobPage.fill(selectors.contacts.contactNickname, 'Alice');
    await bobPage.click(selectors.contacts.addFriendSubmit);
    
    // Verify contact appears
    await expect(bobPage.locator('text=Alice')).toBeVisible({ timeout: 10000 });
    
    await bobPage.close();
  });

  test('should create share group', async ({ page }) => {
    const alice = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music');
    
    await login(page, alice.apiUrl, 'admin', 'admin');

    await page.goto(`${alice.apiUrl}/sharegroups`);
    await page.waitForSelector(selectors.shareGroups.createGroup, { timeout: 10000 });
    
    // Click create group button
    await page.click(selectors.shareGroups.createGroup);
    
    // Fill group name
    await page.fill(selectors.shareGroups.groupNameInput, 'Test Group');
    
    // Submit
    await page.click(selectors.shareGroups.createGroupSubmit);
    
    // Verify group appears in list
    await expect(page.locator('text=Test Group')).toBeVisible({ timeout: 10000 });
  });

  test('should create collection and share to group', async ({ page, context }) => {
    test.skip(true, 'TODO: complete end-to-end collection sharing flow');

    const alice = await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music');
    const bob = await harness.startNode('bob', 'test-data/slskdn-test-fixtures/book');

    // Alice creates group
    await login(page, alice.apiUrl, 'admin', 'admin');
    await page.goto(`${alice.apiUrl}/sharegroups`);
    
    await page.click(selectors.shareGroups.createGroup);
    await page.fill(selectors.shareGroups.groupNameInput, 'Test Group');
    await page.click(selectors.shareGroups.createGroupSubmit);
    await expect(page.locator('text=Test Group')).toBeVisible({ timeout: 10000 });
    
    // Get group ID (from URL or DOM)
    // TODO: Extract group ID from the created group row
    
    // Alice creates collection
    // TODO: Navigate to collections, create collection, add items
    
    // Alice shares collection to group
    // TODO: Navigate to shares, create share grant for group
    
    // Bob should see shared content
    const bobPage = await context.newPage();
    await bobPage.goto(`${bob.apiUrl}/shared`);
    await login(bobPage, bob.apiUrl, 'admin', 'admin');
    
    // TODO: Verify shared content appears
    // await expect(bobPage.locator(selectors.sharedWithMe.sharedItem)).toBeVisible();
    
    await bobPage.close();
  });
});
