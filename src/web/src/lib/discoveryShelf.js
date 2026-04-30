import {
  getLocalStorageItem,
  setLocalStorageItem,
} from './storage';
import { getPlayerRatingKey } from './playerRatings';

export const discoveryShelfStorageKey = 'slskdn.discovery.shelf';

const maxShelfItems = 200;

const normalizeText = (value = '') => String(value).trim();

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
