/**
 * Centralized selector map for E2E tests.
 * 
 * All selectors use data-testid attributes for stability.
 * Update this file when adding new test IDs to components.
 */
export const T = {
  // Auth
  loginUsername: "login-username",
  loginPassword: "login-password",
  loginSubmit: "login-submit",
  logout: "logout",

  // Nav
  navSystem: "nav-system",
  navSearch: "nav-search",
  navDownloads: "nav-downloads",
  navUploads: "nav-uploads",
  navUsers: "nav-users",
  navChat: "nav-chat",
  navRooms: "nav-rooms",
  navContacts: "nav-contacts",
  navGroups: "nav-groups",
  navCollections: "nav-collections",
  navShares: "nav-shares",
  navSharedWithMe: "nav-shared-with-me",
  navBrowse: "nav-browse",

  // System tabs (if needed)
  systemTabShares: "system-tab-shares",

  // Contacts / Invites
  contactsCreateInvite: "contacts-create-invite",
  contactsInviteOutput: "contacts-invite-output",
  contactsInviteFriendCode: "contacts-invite-friend-code",
  contactsAddFriend: "contacts-add-friend",
  contactsAddInviteInput: "contacts-add-invite-input",
  contactsContactNickname: "contacts-contact-nickname",
  contactsAddInviteSubmit: "contacts-add-invite-submit",
  contactsRow: (peerLabel: string) => `contact-row-${peerLabel}`,

  // Groups
  groupsCreate: "groups-create",
  groupsNameInput: "groups-name-input",
  groupsCreateSubmit: "groups-create-submit",
  groupRow: (groupName: string) => `group-row-${groupName}`,
  groupAddMember: "group-add-member",
  groupMemberPicker: "group-member-picker",
  groupMemberAddSubmit: "group-member-add-submit",

  // Collections / Shares
  collectionsCreate: "collections-create",
  collectionsTypeSelect: "collections-type-select",
  collectionsTitleInput: "collections-title-input",
  collectionsCreateSubmit: "collections-create-submit",
  collectionRow: (title: string) => `collection-row-${title}`,
  collectionAddItem: "collection-add-item",
  collectionItemPicker: "collection-item-picker",
  collectionAddItemSubmit: "collection-add-item-submit",

  shareCreate: "share-create",
  shareAudiencePicker: "share-audience-picker",
  sharePolicyStream: "share-policy-stream",
  sharePolicyDownload: "share-policy-download",
  shareCreateSubmit: "share-create-submit",

  // Recipient
  incomingShareRow: (title: string) => `incoming-share-row-${title}`,
  incomingShareOpen: "incoming-share-open",
  incomingStreamButton: "incoming-stream",
  incomingBackfillButton: "incoming-backfill",
  downloadRow: (fileName: string) => `download-row-${fileName}`,

  // Page roots (for existence checks)
  pageRoot: "page-root",
  downloadsRoot: "downloads-root",
  uploadsRoot: "uploads-root",
  systemSharesTable: "system-shares-table",
  connectionStatus: "connection-status",
  sharedManifest: "shared-manifest",
  sharesList: "shares-list",
  collectionItems: "collection-items",
  groupMembers: "group-members",
  
  // Search
  searchInput: "search-input",
  searchResult: "search-result",
  
  // Library/Browse
  libraryContent: "library-content",
  browseContent: "browse-content",
  libraryItem: "library-item",
  browseItem: "browse-item",
} as const;
