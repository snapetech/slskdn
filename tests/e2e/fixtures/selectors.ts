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
    username: '[data-testid="login-username"] input',
    password: '[data-testid="login-password"] input',
    submit: '[data-testid="login-submit"]'
  },
  
  // Contacts
  contacts: {
    createInvite: '[data-testid="contacts-create-invite"]',
    addFriend: '[data-testid="contacts-add-friend"]',
    inviteLink: '[data-testid="contacts-invite-output"]',
    inviteFriendCode: '[data-testid="contacts-invite-friend-code"]',
    inviteLinkInput: '[data-testid="contacts-add-invite-input"]',
    contactNickname: '[data-testid="contacts-contact-nickname"]',
    addFriendSubmit: '[data-testid="contacts-add-invite-submit"]'
  },
  
  // Share Groups
  shareGroups: {
    createGroup: '[data-testid="groups-create"]',
    groupNameInput: '[data-testid="groups-name-input"] input',
    createGroupSubmit: '[data-testid="groups-create-submit"]',
    groupRow: 'tr[data-testid^="group-row-"]'
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
