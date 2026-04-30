import {
  getLocalStorageItem,
  setLocalStorageItem,
} from './storage';

export const playerRatingsStorageKey = 'slskdn.player.ratings';

const normalizeText = (value = '') => String(value).trim().toLowerCase();

const readRatings = () => {
  try {
    const parsed = JSON.parse(getLocalStorageItem(playerRatingsStorageKey, '{}'));
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? parsed
      : {};
  } catch {
    return {};
  }
};

const writeRatings = (ratings) => {
  setLocalStorageItem(playerRatingsStorageKey, JSON.stringify(ratings));
  return ratings;
};

export const getPlayerRatingKey = (track = {}) => {
  if (!track) return '';
  if (track.contentId) return `content:${track.contentId}`;
  if (track.streamUrl) return `stream:${track.streamUrl}`;

  const parts = [
    normalizeText(track.artist),
    normalizeText(track.album),
    normalizeText(track.title || track.fileName),
  ].filter(Boolean);

  return parts.length > 0 ? `meta:${parts.join('|')}` : '';
};

export const getPlayerRating = (track) => {
  const key = getPlayerRatingKey(track);
  if (!key) return 0;

  const rating = Number(readRatings()[key] || 0);
  return Number.isInteger(rating) && rating >= 1 && rating <= 5 ? rating : 0;
};

export const setPlayerRating = (track, rating) => {
  const key = getPlayerRatingKey(track);
  if (!key) return getPlayerRating(track);

  const nextRating = Number(rating);
  const ratings = readRatings();

  if (!Number.isInteger(nextRating) || nextRating < 1 || nextRating > 5) {
    delete ratings[key];
  } else {
    ratings[key] = nextRating;
  }

  writeRatings(ratings);
  return getPlayerRating(track);
};

export const getPlayerRatingSummary = (track) => {
  const rating = getPlayerRating(track);

  if (rating >= 4) {
    return {
      label: 'Discovery boost',
      rating,
      tone: 'positive',
    };
  }

  if (rating > 0 && rating <= 2) {
    return {
      label: 'Discovery caution',
      rating,
      tone: 'negative',
    };
  }

  if (rating === 3) {
    return {
      label: 'Neutral rating',
      rating,
      tone: 'neutral',
    };
  }

  return {
    label: 'Not rated',
    rating: 0,
    tone: 'unrated',
  };
};
