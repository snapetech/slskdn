/**
 * Centralized selectors for E2E tests.
 * 
 * Prefer data-testid attributes for stability.
 * Update this file when adding new test IDs to components.
 */
export const selectors = {
  // Navigation
  nav: {
    contacts: '[data-testid="nav-contacts"]',
    shareGroups: '[data-testid="nav-sharegroups"]',
    sharedWithMe: '[data-testid="nav-shared-with-me"]',
    search: '[data-testid="nav-search"]'
  },
  
  // Login
  login: {
    username: '[data-testid="login-username"]',
    password: '[data-testid="login-password"]',
    submit: '[data-testid="login-submit"]'
  },
  
  // Contacts
  contacts: {
    createInvite: '[data-testid="contacts-create-invite"]',
    addFriend: '[data-testid="contacts-add-friend"]',
    inviteLink: '[data-testid="invite-link"]',
    inviteFriendCode: '[data-testid="invite-friend-code"]',
    inviteLinkInput: '[data-testid="invite-link-input"]',
    contactNickname: '[data-testid="contact-nickname"]',
    addFriendSubmit: '[data-testid="add-friend-submit"]'
  },
  
  // Share Groups
  shareGroups: {
    createGroup: '[data-testid="sharegroups-create-group"]',
    groupNameInput: '[data-testid="group-name-input"]',
    createGroupSubmit: '[data-testid="create-group-submit"]',
    groupRow: '[data-testid="sharegroup-row"]'
  },
  
  // Collections
  collections: {
    createCollection: '[data-testid="collections-create"]',
    collectionTitle: '[data-testid="collection-title"]',
    createCollectionSubmit: '[data-testid="create-collection-submit"]'
  },
  
  // Shared with Me
  sharedWithMe: {
    sharedItem: '[data-testid="shared-item"]',
    streamButton: '[data-testid="stream-button"]',
    downloadButton: '[data-testid="download-button"]'
  }
};
