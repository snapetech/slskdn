/**
 * Centralized selector map for E2E tests.
 *
 * All selectors use data-testid attributes for stability.
 * Update this file when adding new test IDs to components.
 */
export const T = {
  contactsAddFriend: 'contacts-add-friend',

  contactsAddInviteInput: 'contacts-add-invite-input',

  contactsAddInviteSubmit: 'contacts-add-invite-submit',

  contactsContactNickname: 'contacts-contact-nickname',

  // Contacts / Invites
  contactsCreateInvite: 'contacts-create-invite',

  contactsInviteFriendCode: 'contacts-invite-friend-code',

  contactsInviteOutput: 'contacts-invite-output',

  contactsRow: (peerLabel: string) => `contact-row-${peerLabel}`,

  // Collections / Shares
collectionsCreate: 'collections-create',

  
loginPassword: 'login-password',

  loginSubmit: 'login-submit',

  collectionRow: (title: string) => `collection-row-${title}`,

  
collectionAddItem: 'collection-add-item',

  // Auth
loginUsername: 'login-username',

  collectionAddItemSubmit: 'collection-add-item-submit',

  logout: 'logout',

  collectionItemPicker: 'collection-item-search-input',

  navBrowse: 'nav-browse',

  collectionItemResults: 'collection-item-results',

  navChat: 'nav-chat',

  collectionsCreateSubmit: 'collections-create-submit',

  navCollections: 'nav-collections',

  collectionsTitleInput: 'collections-title-input',

  navContacts: 'nav-contacts',

  collectionsTypeSelect: 'collections-type-select',

  navDownloads: 'nav-downloads',

  downloadRow: (fileName: string) => `download-row-${fileName}`,

  navGroups: 'nav-groups',

  groupAddMember: 'group-add-member',

  navSearch: 'nav-search',

  downloadsRoot: 'downloads-root',

  
connectionStatus: 'connection-status',

  // Nav
navSystem: 'nav-system',

  groupMemberAddSubmit: 'group-member-add-submit',

  navUploads: 'nav-uploads',

  collectionItems: 'collection-items',

  navUsers: 'nav-users',

  groupMemberPicker: 'group-member-picker',

  navRooms: 'nav-rooms',

  browseContent: 'browse-content',

  navSharedWithMe: 'nav-shared-with-me',

  browseItem: 'browse-item',

  navShares: 'nav-shares',

  groupMembers: 'group-members',

  
groupRow: (groupName: string) => `group-row-${groupName}`,

  // System tabs (if needed)
systemTabShares: 'system-tab-shares',

  // Groups
  groupsCreate: 'groups-create',

  groupsCreateSubmit: 'groups-create-submit',

  groupsNameInput: 'groups-name-input',

  incomingBackfillButton: 'incoming-backfill',

  incomingShareOpen: 'incoming-share-open',
  // Recipient
  incomingShareRow: (title: string) => `incoming-share-row-${title}`,

  incomingStreamButton: 'incoming-stream',

  // Library/Browse
  libraryContent: 'library-content',

  libraryItem: 'library-item',

  // Page roots (for existence checks)
  pageRoot: 'page-root',

  // Search
  searchInput: 'search-input',

  searchResult: 'search-result',

  shareAudiencePicker: 'share-audience-picker',

  shareCreate: 'share-create',

  shareCreateSubmit: 'share-create-submit',

  sharedManifest: 'shared-manifest',

  sharePolicyDownload: 'share-policy-download',

  sharePolicyStream: 'share-policy-stream',

  sharesList: 'shares-list',

  systemSharesTable: 'system-shares-table',
  uploadsRoot: 'uploads-root',
} as const;
