/**
 * Centralized selector map for E2E tests.
 *
 * All selectors use data-testid attributes for stability.
 * Update this file when adding new test IDs to components.
 */
export const T = {
  // Contacts / Invites
contactsCreateInvite: 'contacts-create-invite',

  
loginPassword: 'login-password',

  loginSubmit: 'login-submit',

  
  contactsInviteFriendCode: 'contacts-invite-friend-code',

  // Auth
loginUsername: 'login-username',

  contactsAddFriend: 'contacts-add-friend',

  logout: 'logout',

  contactsAddInviteInput: 'contacts-add-invite-input',

  navBrowse: 'nav-browse',

  contactsAddInviteSubmit: 'contacts-add-invite-submit',

  navChat: 'nav-chat',

  contactsContactNickname: 'contacts-contact-nickname',

  navCollections: 'nav-collections',

  contactsInviteOutput: 'contacts-invite-output',

  navContacts: 'nav-contacts',

  contactsRow: (peerLabel: string) => `contact-row-${peerLabel}`,

  navDownloads: 'nav-downloads',

  // Collections / Shares
collectionsCreate: 'collections-create',

  
navGroups: 'nav-groups',

  navSearch: 'nav-search',

  collectionRow: (title: string) => `collection-row-${title}`,

  
collectionAddItem: 'collection-add-item',

  // Nav
navSystem: 'nav-system',

  collectionAddItemSubmit: 'collection-add-item-submit',

  navUploads: 'nav-uploads',

  collectionItemPicker: 'collection-item-search-input',

  navUsers: 'nav-users',

  collectionItemResults: 'collection-item-results',

  navRooms: 'nav-rooms',

  collectionsCreateSubmit: 'collections-create-submit',

  navSharedWithMe: 'nav-shared-with-me',

  collectionsTitleInput: 'collections-title-input',

  navShares: 'nav-shares',

  collectionsTypeSelect: 'collections-type-select',

  
downloadRow: (fileName: string) => `download-row-${fileName}`,

  // System tabs (if needed)
systemTabShares: 'system-tab-shares',

  groupAddMember: 'group-add-member',

  downloadsRoot: 'downloads-root',

  groupMemberAddSubmit: 'group-member-add-submit',

  groupMemberPicker: 'group-member-picker',

  connectionStatus: 'connection-status',
  
groupRow: (groupName: string) => `group-row-${groupName}`,

  // Groups
groupsCreate: 'groups-create',

  collectionItems: 'collection-items',

  groupMembers: 'group-members',

  groupsCreateSubmit: 'groups-create-submit',

  browseContent: 'browse-content',

  groupsNameInput: 'groups-name-input',

  incomingBackfillButton: 'incoming-backfill',

  incomingShareOpen: 'incoming-share-open',
  
  browseItem: 'browse-item',
  // Recipient
incomingShareRow: (title: string) => `incoming-share-row-${title}`,

  incomingStreamButton: 'incoming-stream',

  // Library/Browse
  libraryContent: 'library-content',

  shareAudiencePicker: 'share-audience-picker',

  libraryItem: 'library-item',

  shareCreate: 'share-create',

  // Page roots (for existence checks)
pageRoot: 'page-root',

  
shareCreateSubmit: 'share-create-submit',

  // Search
searchInput: 'search-input',

  
  sharePolicyDownload: 'share-policy-download',

  searchResult: 'search-result',

  sharedManifest: 'shared-manifest',

  sharePolicyStream: 'share-policy-stream',

  sharesList: 'shares-list',

  systemSharesTable: 'system-shares-table',
  uploadsRoot: 'uploads-root',
} as const;
