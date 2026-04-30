import {
  getLocalStorageItem,
  setLocalStorageItem,
} from './storage';

export const listeningHistoryStorageKey = 'slskdn.listening.history';

const maxHistoryEntries = 500;

const normalizeText = (value = '') => String(value).trim();

const readHistory = () => {
  try {
    const parsed = JSON.parse(getLocalStorageItem(listeningHistoryStorageKey, '[]'));
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
};

const writeHistory = (entries) => {
  const normalized = entries
    .filter((entry) => entry?.contentId || entry?.title)
    .slice(0, maxHistoryEntries);
  setLocalStorageItem(listeningHistoryStorageKey, JSON.stringify(normalized));
  return normalized;
};

const getTrackKey = (track = {}) =>
  track.contentId ||
  [
    normalizeText(track.artist).toLowerCase(),
    normalizeText(track.album).toLowerCase(),
    normalizeText(track.title || track.fileName).toLowerCase(),
  ].filter(Boolean).join('|');

const incrementCount = (map, key) => {
  if (!key) return;
  map.set(key, (map.get(key) || 0) + 1);
};

const topCounts = (map, limit) =>
  Array.from(map.entries())
    .map(([label, plays]) => ({ label, plays }))
    .sort((left, right) => right.plays - left.plays || left.label.localeCompare(right.label))
    .slice(0, limit);

const filterByRange = (history, rangeDays, now) => {
  if (!rangeDays) return history;

  const nowTime = Date.parse(now instanceof Date ? now.toISOString() : now);
  if (!Number.isFinite(nowTime)) return history;

  const cutoff = nowTime - rangeDays * 24 * 60 * 60 * 1000;
  return history.filter((entry) => {
    const playedAt = Date.parse(entry.playedAt || '');
    return Number.isFinite(playedAt) && playedAt >= cutoff;
  });
};

const getForgottenFavorites = (history, rangeDays, now, limit) => {
  const nowTime = Date.parse(now instanceof Date ? now.toISOString() : now);
  if (!Number.isFinite(nowTime)) return [];

  const cutoffDays = rangeDays || 30;
  const cutoff = nowTime - cutoffDays * 24 * 60 * 60 * 1000;
  const byTrack = new Map();

  history.forEach((entry) => {
    const key = getTrackKey(entry);
    if (!key) return;

    const existing = byTrack.get(key) || {
      album: entry.album,
      artist: entry.artist,
      lastPlayedAt: entry.playedAt,
      plays: 0,
      title: entry.title,
    };
    const previousTime = Date.parse(existing.lastPlayedAt || '');
    const entryTime = Date.parse(entry.playedAt || '');

    byTrack.set(key, {
      ...existing,
      lastPlayedAt:
        Number.isFinite(entryTime) && (!Number.isFinite(previousTime) || entryTime > previousTime)
          ? entry.playedAt
          : existing.lastPlayedAt,
      plays: existing.plays + 1,
    });
  });

  return Array.from(byTrack.values())
    .filter((entry) => {
      const lastPlayed = Date.parse(entry.lastPlayedAt || '');
      return entry.plays >= 2 && Number.isFinite(lastPlayed) && lastPlayed < cutoff;
    })
    .sort((left, right) => right.plays - left.plays || left.title.localeCompare(right.title))
    .slice(0, limit);
};

export const getListeningHistory = () => readHistory();

export const recordLocalPlay = (track = {}, playedAt = new Date().toISOString()) => {
  const title = normalizeText(track.title || track.fileName);
  const contentId = normalizeText(track.contentId);
  if (!title && !contentId) return getListeningHistory();

  const entry = {
    album: normalizeText(track.album),
    artist: normalizeText(track.artist),
    contentId,
    playedAt,
    title: title || contentId,
  };

  const next = [
    entry,
    ...getListeningHistory().filter((existing) => {
      const sameTrack = getTrackKey(existing) === getTrackKey(entry);
      if (!sameTrack) return true;

      const previousTime = Date.parse(existing.playedAt || '');
      const nextTime = Date.parse(playedAt || '');
      if (!Number.isFinite(previousTime) || !Number.isFinite(nextTime)) {
        return true;
      }

      return Math.abs(nextTime - previousTime) > 30_000;
    }),
  ];

  return writeHistory(next);
};

export const clearListeningHistory = () => writeHistory([]);

export const getListeningStats = ({
  limit = 5,
  now = new Date(),
  rangeDays = null,
} = {}) => {
  const history = getListeningHistory();
  const rangedHistory = filterByRange(history, rangeDays, now);
  const artists = new Map();
  const albums = new Map();
  const tracks = new Map();

  rangedHistory.forEach((entry) => {
    incrementCount(artists, entry.artist);
    incrementCount(albums, entry.album);
    incrementCount(tracks, entry.title);
  });

  return {
    forgottenFavorites: getForgottenFavorites(history, rangeDays, now, limit),
    history,
    rangeDays,
    recent: rangedHistory.slice(0, limit),
    topAlbums: topCounts(albums, limit),
    topArtists: topCounts(artists, limit),
    topTracks: topCounts(tracks, limit),
    totalPlays: rangedHistory.length,
  };
};
