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

const getGenreValues = (track = {}) => [
  normalizeText(track.genre),
  ...(Array.isArray(track.genres) ? track.genres.map(normalizeText) : []),
  ...(Array.isArray(track.tags) ? track.tags.map(normalizeText) : []),
].filter(Boolean);

const normalizePlayedAt = (value, fallback = null) => {
  const time = Date.parse(value || '');
  return Number.isFinite(time) ? new Date(time).toISOString() : fallback;
};

const topCounts = (map, limit) =>
  Array.from(map.entries())
    .map(([label, plays]) => ({ label, plays }))
    .sort((left, right) => right.plays - left.plays || left.label.localeCompare(right.label))
    .slice(0, limit);

const addRecommendationSeed = (seeds, seen, seed) => {
  const query = normalizeText(seed.query);
  if (!query) return;

  const key = query.toLowerCase();
  if (seen.has(key)) return;

  seen.add(key);
  seeds.push({
    ...seed,
    query,
  });
};

const parseCsvLine = (line) => {
  const cells = [];
  let current = '';
  let quoted = false;

  for (let index = 0; index < line.length; index += 1) {
    const character = line[index];
    const next = line[index + 1];

    if (character === '"' && quoted && next === '"') {
      current += '"';
      index += 1;
    } else if (character === '"') {
      quoted = !quoted;
    } else if (character === ',' && !quoted) {
      cells.push(current.trim());
      current = '';
    } else {
      current += character;
    }
  }

  cells.push(current.trim());
  return cells;
};

const getFirstValue = (item, keys) => {
  const key = keys.find((candidate) => normalizeText(item[candidate]));
  return key ? normalizeText(item[key]) : '';
};

const normalizeImportItem = (item = {}, source = 'media-server-import') => {
  const title = getFirstValue(item, ['title', 'trackTitle', 'track', 'name', 'fileName']);
  const contentId = getFirstValue(item, ['contentId', 'content_id', 'hash']);
  const playedAt = normalizePlayedAt(
    getFirstValue(item, ['playedAt', 'played_at', 'lastPlayedAt', 'time', 'timestamp', 'date']),
  );

  if ((!title && !contentId) || !playedAt) return null;

  return {
    album: getFirstValue(item, ['album', 'albumTitle']),
    artist: getFirstValue(item, ['artist', 'artistName', 'albumArtist']),
    contentId,
    genres: getGenreValues(item).slice(0, 8),
    playedAt,
    source,
    title: title || contentId,
  };
};

const parseJsonImport = (raw) => {
  const parsed = JSON.parse(raw);
  if (Array.isArray(parsed)) return parsed;
  if (Array.isArray(parsed.history)) return parsed.history;
  if (Array.isArray(parsed.items)) return parsed.items;
  if (Array.isArray(parsed.plays)) return parsed.plays;
  return [];
};

const parseCsvImport = (raw) => {
  const lines = raw
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);

  if (lines.length < 2) return [];

  const headers = parseCsvLine(lines[0]).map((header) => header.trim());
  return lines.slice(1).map((line) => {
    const values = parseCsvLine(line);
    return headers.reduce((item, header, index) => ({
      ...item,
      [header]: values[index] || '',
    }), {});
  });
};

const parseHistoryImport = (raw) => {
  const content = normalizeText(raw);
  if (!content) return [];

  try {
    return parseJsonImport(content);
  } catch {
    return parseCsvImport(content);
  }
};

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
    genres: getGenreValues(track).slice(0, 8),
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

export const importListeningHistory = (raw, source = 'media-server-import') => {
  const existing = getListeningHistory();
  const seen = new Set(existing.map((entry) => `${getTrackKey(entry)}|${entry.playedAt}`));
  const imported = [];

  parseHistoryImport(raw).forEach((item) => {
    const entry = normalizeImportItem(item, source);
    if (!entry) return;

    const key = `${getTrackKey(entry)}|${entry.playedAt}`;
    if (seen.has(key)) return;

    seen.add(key);
    imported.push(entry);
  });

  const history = writeHistory(
    [...imported, ...existing].sort((left, right) => {
      const leftTime = Date.parse(left.playedAt || '');
      const rightTime = Date.parse(right.playedAt || '');
      return (Number.isFinite(rightTime) ? rightTime : 0) - (Number.isFinite(leftTime) ? leftTime : 0);
    }),
  );

  return {
    history,
    imported: imported.length,
    skipped: Math.max(parseHistoryImport(raw).length - imported.length, 0),
  };
};

export const exportListeningHistoryJson = () =>
  JSON.stringify(getListeningHistory(), null, 2);

export const exportListeningHistoryCsv = () => {
  const escapeCell = (value = '') => {
    const text = normalizeText(value);
    return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
  };

  return [
    'playedAt,artist,album,title,genre,contentId,source',
    ...getListeningHistory().map((entry) => [
      entry.playedAt,
      entry.artist,
      entry.album,
      entry.title,
      (entry.genres || []).join('; '),
      entry.contentId,
      entry.source,
    ].map(escapeCell).join(',')),
  ].join('\n');
};

export const getListeningStats = ({
  limit = 5,
  now = new Date(),
  rangeDays = null,
} = {}) => {
  const history = getListeningHistory();
  const rangedHistory = filterByRange(history, rangeDays, now);
  const artists = new Map();
  const albums = new Map();
  const genres = new Map();
  const tracks = new Map();

  rangedHistory.forEach((entry) => {
    incrementCount(artists, entry.artist);
    incrementCount(albums, entry.album);
    getGenreValues(entry).forEach((genre) => incrementCount(genres, genre));
    incrementCount(tracks, entry.title);
  });

  return {
    forgottenFavorites: getForgottenFavorites(history, rangeDays, now, limit),
    history,
    rangeDays,
    recent: rangedHistory.slice(0, limit),
    topAlbums: topCounts(albums, limit),
    topArtists: topCounts(artists, limit),
    topGenres: topCounts(genres, limit),
    topTracks: topCounts(tracks, limit),
    totalPlays: rangedHistory.length,
  };
};

export const getListeningRecommendationSeeds = (stats = {}, limit = 5) => {
  const seeds = [];
  const seen = new Set();

  (stats.forgottenFavorites || []).forEach((track) => {
    addRecommendationSeed(seeds, seen, {
      basis: `${track.plays} older plays`,
      label: track.title,
      query: [track.artist, track.title].filter(Boolean).join(' '),
      type: 'Forgotten favorite',
    });
  });

  (stats.topArtists || []).forEach((artist) => {
    addRecommendationSeed(seeds, seen, {
      basis: `${artist.plays} local plays`,
      label: artist.label,
      query: artist.label,
      type: 'Artist seed',
    });
  });

  (stats.topGenres || []).forEach((genre) => {
    addRecommendationSeed(seeds, seen, {
      basis: `${genre.plays} tagged plays`,
      label: genre.label,
      query: genre.label,
      type: 'Genre seed',
    });
  });

  return seeds.slice(0, limit);
};
