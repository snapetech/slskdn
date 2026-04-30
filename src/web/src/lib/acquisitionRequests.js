import {
  addDiscoveryInboxItem,
  getDiscoveryInboxItems,
} from './discoveryInbox';

export const acquisitionRequestStates = {
  approved: {
    color: 'green',
    label: 'Approved',
    summary: 'Approved in Discovery Inbox for the next acquisition step.',
  },
  automatic: {
    color: 'blue',
    label: 'Automatic',
    summary: 'Enabled and allowed to auto-download through Wishlist policy.',
  },
  disabled: {
    color: 'grey',
    label: 'Disabled',
    summary: 'Saved but not eligible for scheduled wishlist processing.',
  },
  failed: {
    color: 'red',
    label: 'Failed',
    summary: 'A previous acquisition/import attempt failed and needs review.',
  },
  imported: {
    color: 'teal',
    label: 'Imported',
    summary: 'The request has been imported into the library.',
  },
  rejected: {
    color: 'red',
    label: 'Rejected',
    summary: 'Rejected in Discovery Inbox and suppressed for the same evidence.',
  },
  review: {
    color: 'yellow',
    label: 'Review',
    summary: 'Waiting in Discovery Inbox for approval or rejection.',
  },
  snoozed: {
    color: 'grey',
    label: 'Snoozed',
    summary: 'Snoozed in Discovery Inbox until later review.',
  },
  staged: {
    color: 'violet',
    label: 'Staged',
    summary: 'Downloaded or prepared for import review.',
  },
  wanted: {
    color: 'purple',
    label: 'Wanted',
    summary: 'Enabled wishlist request with manual download approval.',
  },
};

const normalizeText = (value) => `${value || ''}`.trim().toLowerCase();

export const getWishlistEvidenceKey = (item) =>
  `wishlist:${item.id || normalizeText(item.searchText)}:${normalizeText(item.searchText)}:${normalizeText(item.filter)}`;

const mapInboxStateToRequestState = (state) => {
  switch (state) {
    case 'Approved':
      return 'approved';
    case 'Downloading':
      return 'automatic';
    case 'Failed':
      return 'failed';
    case 'Imported':
      return 'imported';
    case 'Rejected':
      return 'rejected';
    case 'Snoozed':
      return 'snoozed';
    case 'Staged':
      return 'staged';
    default:
      return 'review';
  }
};

export const getWishlistRequestState = (
  item,
  inboxItems = getDiscoveryInboxItems(),
) => {
  const inboxItem = inboxItems.find(
    (candidate) => candidate.evidenceKey === getWishlistEvidenceKey(item),
  );

  if (inboxItem) {
    return acquisitionRequestStates[mapInboxStateToRequestState(inboxItem.state)];
  }

  if (!item.enabled) {
    return acquisitionRequestStates.disabled;
  }

  if (item.autoDownload) {
    return acquisitionRequestStates.automatic;
  }

  return acquisitionRequestStates.wanted;
};

export const buildWishlistDiscoveryInboxItem = (item) => ({
  evidenceKey: getWishlistEvidenceKey(item),
  networkImpact: item.autoDownload
    ? 'Review only; approving here does not start download work. This Wishlist item is configured for auto-download when its scheduler runs.'
    : 'Review only; approving here does not start peer search, browse, or download work.',
  reason: `Saved Wishlist request${item.filter ? ` with filter "${item.filter}"` : ''}.`,
  searchText: item.searchText,
  source: 'Wishlist',
  sourceId: item.id,
  title: item.searchText,
});

export const addWishlistItemToDiscoveryInbox = (item) =>
  addDiscoveryInboxItem(buildWishlistDiscoveryInboxItem(item));

export const buildWishlistRequestSummary = ({
  inboxItems = getDiscoveryInboxItems(),
  items = [],
  quota = 25,
} = {}) => {
  const counts = items.reduce(
    (summary, item) => {
      const state = getWishlistRequestState(item, inboxItems).label;
      summary.byState[state] = (summary.byState[state] || 0) + 1;
      if (item.enabled) summary.enabled += 1;
      if (item.autoDownload) summary.automatic += 1;
      return summary;
    },
    {
      automatic: 0,
      byState: {},
      enabled: 0,
      total: items.length,
    },
  );

  return {
    ...counts,
    quota,
    quotaRemaining: Math.max(quota - items.length, 0),
    quotaStatus: items.length > quota ? 'Over quota' : 'Within quota',
    reviewCount:
      (counts.byState.Review || 0) +
      (counts.byState.Approved || 0) +
      (counts.byState.Snoozed || 0),
  };
};
