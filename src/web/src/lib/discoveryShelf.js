import {
  getLocalStorageItem,
  setLocalStorageItem,
} from './storage';
import { getPlayerRatingKey } from './playerRatings';

export const discoveryShelfStorageKey = 'slskdn.discovery.shelf';

const maxShelfItems = 200;

const normalizeText = (value = '') => String(value).trim();

const normalizePositiveInteger = (value, fallback) => {
  const number = Number(value);
  return Number.isInteger(number) && number > 0 ? number : fallback;
};

const readShelf = () => {
  try {
    const parsed = JSON.parse(getLocalStorageItem(discoveryShelfStorageKey, '[]'));
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
};

const writeShelf = (items) => {
  const normalized = items
    .filter((item) => item?.key && item?.title)
    .slice(0, maxShelfItems);
  setLocalStorageItem(discoveryShelfStorageKey, JSON.stringify(normalized));
  return normalized;
};

export const getDiscoveryShelfAction = (rating = 0) => {
  if (rating >= 4) return 'promote-preview';
  if (rating > 0 && rating <= 2) return 'archive-preview';
  if (rating === 3) return 'keep-reviewing';
  return 'expiry-watch';
};

export const getDiscoveryShelfActionLabel = (action) => {
  switch (action) {
    case 'promote-preview':
      return 'Promote preview';
    case 'archive-preview':
      return 'Archive preview';
    case 'keep-reviewing':
      return 'Keep reviewing';
    default:
      return 'Expiry watch';
  }
};

export const getDiscoveryShelf = () => readShelf();

export const upsertDiscoveryShelfItem = (
  track = {},
  rating = 0,
  reviewedAt = new Date().toISOString(),
) => {
  const key = getPlayerRatingKey(track);
  const title = normalizeText(track.title || track.fileName);
  if (!key || !title) return getDiscoveryShelf();

  const existing = getDiscoveryShelf().filter((item) => item.key !== key);
  const item = {
    action: getDiscoveryShelfAction(rating),
    album: normalizeText(track.album),
    artist: normalizeText(track.artist),
    contentId: normalizeText(track.contentId),
    key,
    rating: Number.isInteger(Number(rating)) ? Number(rating) : 0,
    reviewedAt,
    sourceProviders: Array.isArray(track.sourceProviders)
      ? track.sourceProviders.slice(0, 6)
      : [],
    title,
  };

  return writeShelf([item, ...existing]);
};

export const removeDiscoveryShelfItem = (key) =>
  writeShelf(getDiscoveryShelf().filter((item) => item.key !== key));

export const clearDiscoveryShelf = () => writeShelf([]);

export const getDiscoveryShelfSummary = () => {
  const items = getDiscoveryShelf();
  return items.reduce((summary, item) => ({
    ...summary,
    [item.action]: (summary[item.action] || 0) + 1,
    total: summary.total + 1,
  }), {
    'archive-preview': 0,
    'expiry-watch': 0,
    'keep-reviewing': 0,
    'promote-preview': 0,
    total: 0,
  });
};

export const buildDiscoveryShelfInboxItem = (item = {}) => {
  const title = [
    item.artist,
    item.album,
    item.title,
  ].map(normalizeText).filter(Boolean).join(' - ') || normalizeText(item.title);

  return {
    evidenceKey: `discovery-shelf:${item.key || item.contentId || title}`,
    networkImpact:
      'Review only; approving here does not start search, peer browse, download, rating sync, sharing, or file mutation.',
    reason: `Player rating ${item.rating || 'unrated'} produced ${getDiscoveryShelfActionLabel(item.action).toLowerCase()}.`,
    searchText: [
      item.artist,
      item.album,
      item.title,
    ].map(normalizeText).filter(Boolean).join(' '),
    source: 'Discovery Shelf',
    sourceId: item.key || item.contentId || title,
    title,
  };
};

export const getDiscoveryShelfPromoteItems = (
  items = getDiscoveryShelf(),
  { limit = 10 } = {},
) =>
  items
    .filter((item) => item.action === 'promote-preview')
    .slice(0, limit)
    .map(buildDiscoveryShelfInboxItem);

export const getDiscoveryShelfPolicyPreview = ({
  expiryDays = 14,
  items = getDiscoveryShelf(),
  now = new Date(),
  requireConsensus = true,
} = {}) => {
  const normalizedExpiryDays = normalizePositiveInteger(expiryDays, 14);
  const nowTime = Date.parse(now instanceof Date ? now.toISOString() : now);
  const cutoff = Number.isFinite(nowTime)
    ? nowTime - normalizedExpiryDays * 24 * 60 * 60 * 1000
    : null;

  return items.reduce((preview, item) => {
    const reviewedAt = Date.parse(item.reviewedAt || '');
    const expired = item.action === 'expiry-watch'
      && cutoff !== null
      && Number.isFinite(reviewedAt)
      && reviewedAt < cutoff;

    if (item.action === 'promote-preview') {
      return {
        ...preview,
        promote: preview.promote + 1,
      };
    }

    if (item.action === 'archive-preview') {
      return {
        ...preview,
        archive: preview.archive + 1,
        blockedByConsensus: preview.blockedByConsensus + (requireConsensus ? 1 : 0),
      };
    }

    if (expired) {
      return {
        ...preview,
        blockedByConsensus: preview.blockedByConsensus + (requireConsensus ? 1 : 0),
        expire: preview.expire + 1,
      };
    }

    return {
      ...preview,
      review: preview.review + 1,
    };
  }, {
    archive: 0,
    blockedByConsensus: 0,
    canApply: false,
    expire: 0,
    expiryDays: normalizedExpiryDays,
    promote: 0,
    requireConsensus,
    review: 0,
  });
};

export const exportDiscoveryShelfPolicyReport = ({
  expiryDays = 14,
  items = getDiscoveryShelf(),
  now = new Date(),
  requireConsensus = true,
} = {}) => {
  const preview = getDiscoveryShelfPolicyPreview({
    expiryDays,
    items,
    now,
    requireConsensus,
  });
  const lines = [
    'Discovery Shelf Policy Preview',
    `Generated: ${now instanceof Date ? now.toISOString() : now}`,
    `Expiry window: ${preview.expiryDays} days`,
    `Consensus required for destructive actions: ${preview.requireConsensus ? 'yes' : 'no'}`,
    `Promote candidates: ${preview.promote}`,
    `Archive candidates: ${preview.archive}`,
    `Expire candidates: ${preview.expire}`,
    `Review candidates: ${preview.review}`,
    `Consensus gated: ${preview.blockedByConsensus}`,
    '',
    'Items:',
    ...items.map((item) => [
      `- ${getDiscoveryShelfActionLabel(item.action)}: ${item.title}`,
      item.artist ? ` by ${item.artist}` : '',
      item.album ? ` (${item.album})` : '',
      ` [rating ${item.rating || 'unrated'}]`,
    ].join('')),
  ];

  return lines.join('\n');
};
