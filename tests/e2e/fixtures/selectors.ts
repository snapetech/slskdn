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
    groupRow: (name: string) => `[data-testid="group-row-${name}"]`,
    groupAddMember: '[data-testid="group-add-member"]',
    groupMemberPicker: '[data-testid="group-member-picker"]',
    groupMemberAddSubmit: '[data-testid="group-member-add-submit"]'
  },
  
  // Collections (match app data-testids)
  collections: {
    createCollection: '[data-testid="collections-create"]',
    collectionTitleInput: '[data-testid="collections-title-input"]',
    collectionsTypeSelect: '[data-testid="collections-type-select"]',
    createCollectionSubmit: '[data-testid="collections-create-submit"]',
    collectionRow: (title: string) => `[data-testid="collection-row-${title}"]`,
    collectionAddItem: '[data-testid="collection-add-item"]',
    collectionItemSearchInput: '[data-testid="collection-item-search-input"]',
    collectionAddItemSubmit: '[data-testid="collection-add-item-submit"]',
    shareCreate: '[data-testid="share-create"]',
    shareAudiencePicker: '[data-testid="share-audience-picker"]',
    shareCreateSubmit: '[data-testid="share-create-submit"]'
  },
  
  // Shared with Me
  sharedWithMe: {
    incomingShareRow: (title: string) => `[data-testid="incoming-share-row-${title}"]`,
    incomingShareOpen: '[data-testid="incoming-share-open"]',
    incomingBackfill: '[data-testid="incoming-backfill"]',
    sharedManifest: '[data-testid="shared-manifest"]',
    sharedItem: '[data-testid="shared-item"]',
    streamButton: '[data-testid="stream-button"]',
    downloadButton: '[data-testid="download-button"]'
  }
};
