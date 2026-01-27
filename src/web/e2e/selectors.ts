/**
 * Centralized selector map for E2E tests.
 *
 * All selectors use data-testid attributes for stability.
 * Update this file when adding new test IDs to components.
 */
export const T = {
  loginPassword: 'login-password',

  loginSubmit: 'login-submit',
  // Auth
  loginUsername: 'login-username',
  logout: 'logout',

  navChat: 'nav-chat',

  navCollections: 'nav-collections',

  navContacts: 'nav-contacts',

  navDownloads: 'nav-downloads',

  navBrowse: 'nav-browse',

  navGroups: 'nav-groups',

  // Contacts / Invites
contactsCreateInvite: 'contacts-create-invite',

  
  navSearch: 'nav-search',

  
  contactsInviteFriendCode: 'contacts-invite-friend-code',

  // Nav
navSystem: 'nav-system',

  contactsAddFriend: 'contacts-add-friend',

  navUploads: 'nav-uploads',

  contactsAddInviteInput: 'contacts-add-invite-input',

  navUsers: 'nav-users',

  contactsAddInviteSubmit: 'contacts-add-invite-submit',

  navRooms: 'nav-rooms',

  contactsContactNickname: 'contacts-contact-nickname',

  navSharedWithMe: 'nav-shared-with-me',

  contactsInviteOutput: 'contacts-invite-output',

  navShares: 'nav-shares',

  contactsRow: (peerLabel: string) => `contact-row-${peerLabel}`,
  
// Collections / Shares
collectionsCreate: 'collections-create',

  
  // System tabs (if needed)
systemTabShares: 'system-tab-shares',

  collectionRow: (title: string) => `collection-row-${title}`,
  groupAddMember: 'group-add-member',

  groupMemberAddSubmit: 'group-member-add-submit',

  collectionAddItem: 'collection-add-item',

  
collectionAddItemSubmit: 'collection-add-item-submit',

  // Groups
groupsCreate: 'groups-create',

  collectionItemPicker: 'collection-item-picker',
  groupsCreateSubmit: 'groups-create-submit',
  collectionsCreateSubmit: 'collections-create-submit',
  groupsNameInput: 'groups-name-input',
  collectionsTitleInput: 'collections-title-input',
  groupRow: (groupName: string) => `group-row-${groupName}`,
  collectionsTypeSelect: 'collections-type-select',
  groupMemberPicker: 'group-member-picker',

  downloadRow: (fileName: string) => `download-row-${fileName}`,

  
incomingBackfillButton: 'incoming-backfill',

  
downloadsRoot: 'downloads-root',

  // Recipient
incomingShareRow: (title: string) => `incoming-share-row-${title}`,

  incomingShareOpen: 'incoming-share-open',

  shareAudiencePicker: 'share-audience-picker',
  connectionStatus: 'connection-status',
  shareCreate: 'share-create',
  collectionItems: 'collection-items',
  sharePolicyDownload: 'share-policy-download',

  groupMembers: 'group-members',

  sharePolicyStream: 'share-policy-stream',

  incomingStreamButton: 'incoming-stream',

  shareCreateSubmit: 'share-create-submit',

  
  browseContent: 'browse-content',

  
  // Library/Browse
libraryContent: 'library-content',
  
browseItem: 'browse-item',
  // Page roots (for existence checks)
pageRoot: 'page-root',
  sharedManifest: 'shared-manifest',

  systemSharesTable: 'system-shares-table',

  libraryItem: 'library-item',

  uploadsRoot: 'uploads-root',

  // Search
  searchInput: 'search-input',

  searchResult: 'search-result',
  sharesList: 'shares-list',
} as const;
