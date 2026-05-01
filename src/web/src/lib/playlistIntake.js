// <copyright file="playlistIntake.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import { getLocalStorageItem, setLocalStorageItem } from './storage';
import { scoreMetadataCandidate } from './metadataMatcher';
import { v4 as uuidv4 } from 'uuid';

export const playlistIntakeStorageKey = 'slskdn.playlistIntake.items';
export const playlistRefreshCadences = ['Manual review', 'Daily', 'Weekly', 'Monthly'];

const now = () => new Date().toISOString();

const normalizeState = (state) =>
  ['Imported', 'Mirrored', 'Rejected'].includes(state) ? state : 'Staged';

const inferProvider = (source = '') => {
  const lower = source.toLowerCase();
  if (lower.includes('youtube') || lower.includes('youtu.be')) return 'YouTube';
  if (lower.includes('spotify')) return 'Spotify';
  if (lower.includes('listenbrainz')) return 'ListenBrainz';
  if (lower.includes('m3u')) return 'M3U';
  if (lower.includes('csv')) return 'CSV';
  return source.startsWith('http') ? 'Provider URL' : 'Local text';
};

const normalizeRefreshCadence = (cadence, mirrorEnabled) => {
  if (!mirrorEnabled) return 'Disabled';
  return playlistRefreshCadences.includes(cadence) ? cadence : 'Manual review';
};

const normalizeCooldownDays = (days) => {
  const parsed = Number.parseInt(days, 10);
  if (Number.isNaN(parsed)) return 7;
  return Math.min(Math.max(parsed, 1), 90);
};

const addDays = (timestamp, days) =>
  new Date(new Date(timestamp).getTime() + days * 24 * 60 * 60 * 1_000).toISOString();

const calculateNextRefreshAt = (playlist, timestamp = now()) => {
  if (!playlist.mirrorEnabled || !playlist.refreshAutomationEnabled) return '';
  return addDays(timestamp, normalizeCooldownDays(playlist.refreshCooldownDays));
};

const parseCsvLine = (line) => {
  const columns = line.split(',').map((column) => column.trim().replace(/^"|"$/g, ''));
  return {
    artist: columns[0] || '',
    title: columns[1] || columns[0] || '',
  };
};

const parseTextLine = (line) => {
  const [artist, title] = line.includes(' - ')
    ? line.split(' - ', 2).map((part) => part.trim())
    : ['', line.trim()];

  return {
    artist,
    title,
  };
};

const parsePlaylistRows = (content = '') => {
  const rows = content
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith('#'));

  return rows.map((line, index) => {
    const parsed = line.includes(',') ? parseCsvLine(line) : parseTextLine(line);
    const title = parsed.title || line;

    return {
      artist: parsed.artist,
      id: uuidv4(),
      lineNumber: index + 1,
      sourceLine: line,
      state: parsed.artist && title ? 'Matched' : 'Unmatched',
      title,
    };
  });
};

const normalizeTrack = (track = {}, index = 0) => ({
  artist: track.artist || '',
  contentId: track.contentId || '',
  durationMs: track.durationMs || null,
  id: track.id || uuidv4(),
  lineNumber: track.lineNumber || index + 1,
  metadataMatch: track.metadataMatch || null,
  sourceLine: track.sourceLine || track.title || '',
  state: ['Matched', 'Rejected', 'Unmatched'].includes(track.state)
    ? track.state
    : 'Unmatched',
  title: track.title || track.sourceLine || 'Untitled track',
});

const normalizePlaylist = (playlist = {}) => {
  const timestamp = now();
  const tracks = Array.isArray(playlist.tracks)
    ? playlist.tracks.map(normalizeTrack)
    : parsePlaylistRows(playlist.content);

  return {
    createdAt: playlist.createdAt || timestamp,
    id: playlist.id || uuidv4(),
    mirrorEnabled: playlist.mirrorEnabled === true,
    name: playlist.name || 'Untitled playlist',
    provider: playlist.provider || inferProvider(playlist.source || playlist.name || ''),
    providerRefreshLimit: Math.min(
      Math.max(Number.parseInt(playlist.providerRefreshLimit, 10) || 500, 1),
      5_000,
    ),
    refreshAutomationEnabled:
      playlist.mirrorEnabled === true && playlist.refreshAutomationEnabled === true,
    refreshCadence: normalizeRefreshCadence(
      playlist.refreshCadence,
      playlist.mirrorEnabled === true,
    ),
    refreshCollectionId: playlist.refreshCollectionId || '',
    refreshCooldownDays: normalizeCooldownDays(playlist.refreshCooldownDays),
    refreshDiff: playlist.refreshDiff || null,
    refreshLastRunAt: playlist.refreshLastRunAt || '',
    refreshNextRunAt: playlist.refreshNextRunAt || '',
    refreshPreview:
      playlist.refreshPreview ||
      'Refresh preview only; no provider fetch, Soulseek search, peer browse, or download has started.',
    source: playlist.source || 'Pasted text',
    state: normalizeState(playlist.state),
    tracks,
    updatedAt: playlist.updatedAt || timestamp,
  };
};

const readPlaylists = (getItem = getLocalStorageItem) => {
  try {
    const parsed = JSON.parse(getItem(playlistIntakeStorageKey, '[]'));
    return Array.isArray(parsed) ? parsed.map(normalizePlaylist) : [];
  } catch {
    return [];
  }
};

const savePlaylists = (playlists, setItem = setLocalStorageItem) => {
  const normalized = playlists.map(normalizePlaylist);
  setItem(playlistIntakeStorageKey, JSON.stringify(normalized));
  return normalized;
};

export const getPlaylistIntakes = () => readPlaylists();

export const addPlaylistIntake = (
  playlist,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const next = normalizePlaylist(playlist);
  const existing = readPlaylists(getItem).filter(
    (item) => item.source.toLowerCase() !== next.source.toLowerCase(),
  );

  return savePlaylists([next, ...existing], setItem);
};

const getTrackKey = (track) =>
  [track.artist, track.title]
    .filter(Boolean)
    .join('|')
    .trim()
    .toLowerCase();

export const buildPlaylistCompletionSummary = (playlist) =>
  playlist.tracks.reduce(
    (summary, track) => ({
      ...summary,
      [track.state]: (summary[track.state] || 0) + 1,
      total: summary.total + 1,
    }),
    {
      Matched: 0,
      Rejected: 0,
      Unmatched: 0,
      total: 0,
    },
  );

export const buildPlaylistRefreshDiff = (playlist, content = '') => {
  const existing = new Map(playlist.tracks.map((track) => [getTrackKey(track), track]));
  const incoming = parsePlaylistRows(content);
  const incomingKeys = new Set(incoming.map(getTrackKey));
  const added = incoming.filter((track) => !existing.has(getTrackKey(track)));
  const removed = playlist.tracks.filter((track) => !incomingKeys.has(getTrackKey(track)));
  const unchanged = incoming.filter((track) => existing.has(getTrackKey(track)));
  const changed = incoming
    .map((track, index) => {
      const previous = playlist.tracks[index];
      return previous && getTrackKey(previous) !== getTrackKey(track)
        ? {
            incoming: track,
            lineNumber: index + 1,
            previous,
          }
        : null;
    })
    .filter(Boolean);

  return {
    added,
    addedCount: added.length,
    changed,
    changedCount: changed.length,
    removed,
    removedCount: removed.length,
    totalIncoming: incoming.length,
    unchangedCount: unchanged.length,
  };
};

export const updatePlaylistIntakeTrackState = (
  playlistId,
  trackId,
  state,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const nextState = ['Matched', 'Rejected'].includes(state) ? state : 'Unmatched';
  const updated = readPlaylists(getItem).map((playlist) =>
    playlist.id === playlistId
      ? {
          ...playlist,
          tracks: playlist.tracks.map((track) =>
            track.id === trackId ? { ...track, state: nextState } : track,
          ),
          updatedAt: now(),
        }
      : playlist,
  );

  return savePlaylists(updated, setItem);
};

export const updatePlaylistRefreshAutomation = (
  playlistId,
  {
    enabled,
    cadence,
    cooldownDays,
    providerRefreshLimit,
  },
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const timestamp = now();
  const updated = readPlaylists(getItem).map((playlist) => {
    if (playlist.id !== playlistId) {
      return playlist;
    }

    const next = {
      ...playlist,
      providerRefreshLimit:
        providerRefreshLimit === undefined
          ? playlist.providerRefreshLimit
          : providerRefreshLimit,
      refreshAutomationEnabled: enabled === true,
      refreshCadence: cadence || playlist.refreshCadence,
      refreshCooldownDays:
        cooldownDays === undefined ? playlist.refreshCooldownDays : cooldownDays,
      updatedAt: timestamp,
    };

    return {
      ...next,
      refreshNextRunAt: calculateNextRefreshAt(next, timestamp),
    };
  });

  return savePlaylists(updated, setItem);
};

export const previewPlaylistIntakeRefresh = (
  playlistId,
  content,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const updated = readPlaylists(getItem).map((playlist) => {
    if (playlist.id !== playlistId) {
      return playlist;
    }

    const refreshDiff = buildPlaylistRefreshDiff(playlist, content);

    return {
      ...playlist,
      refreshDiff,
      refreshPreview:
        `Refresh diff preview only: ${refreshDiff.addedCount} added, ${refreshDiff.removedCount} removed, ${refreshDiff.changedCount} changed, ${refreshDiff.unchangedCount} unchanged. No provider fetch, Soulseek search, peer browse, download, or playlist mutation has started.`,
      updatedAt: now(),
    };
  });

  return savePlaylists(updated, setItem);
};

export const applyPlaylistIntakeRefresh = (
  playlistId,
  content,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
    sourceLabel = 'manual refresh',
  } = {},
) => {
  const timestamp = now();
  const updated = readPlaylists(getItem).map((playlist) => {
    if (playlist.id !== playlistId) {
      return playlist;
    }

    const previousByKey = new Map(
      playlist.tracks.map((track) => [getTrackKey(track), track]),
    );
    const incoming = parsePlaylistRows(content).map((track) => {
      const previous = previousByKey.get(getTrackKey(track));
      return previous
        ? {
            ...track,
            id: previous.id,
            state: previous.state,
          }
        : track;
    });
    const refreshDiff = buildPlaylistRefreshDiff(playlist, content);
    const next = {
      ...playlist,
      refreshDiff,
      refreshLastRunAt: timestamp,
      refreshPreview:
        `Applied ${sourceLabel}: ${refreshDiff.addedCount} added, ${refreshDiff.removedCount} removed, ${refreshDiff.changedCount} changed, ${refreshDiff.unchangedCount} unchanged. No Soulseek search, peer browse, or download was started.`,
      state: 'Mirrored',
      tracks: incoming,
      updatedAt: timestamp,
    };

    return {
      ...next,
      refreshNextRunAt: calculateNextRefreshAt(next, timestamp),
    };
  });

  return savePlaylists(updated, setItem);
};

export const markPlaylistCollectionCreated = (
  playlistId,
  collectionId,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const updated = readPlaylists(getItem).map((playlist) =>
    playlist.id === playlistId
      ? {
          ...playlist,
          refreshCollectionId: collectionId || playlist.refreshCollectionId,
          state: 'Imported',
          updatedAt: now(),
        }
      : playlist,
  );

  return savePlaylists(updated, setItem);
};

export const buildPlaylistIntakeSummary = (playlists = []) =>
  playlists.reduce(
    (summary, playlist) => {
      const unmatched = playlist.tracks.filter(
        (track) => track.state === 'Unmatched',
      ).length;

      return {
        ...summary,
        mirrored: playlist.mirrorEnabled ? summary.mirrored + 1 : summary.mirrored,
        total: summary.total + 1,
        tracks: summary.tracks + playlist.tracks.length,
        unmatched: summary.unmatched + unmatched,
      };
    },
    {
      mirrored: 0,
      total: 0,
      tracks: 0,
      unmatched: 0,
    },
  );

export const buildPlaylistDiscoverySeed = (playlist, track) => ({
  evidenceKey: `playlist:${playlist.id}:${track.lineNumber}:${track.title}`.toLowerCase(),
  networkImpact:
    'Playlist intake review seed only; no provider fetch, Soulseek search, peer browse, or download has started.',
  reason: `Imported from ${playlist.name} (${playlist.provider}) line ${track.lineNumber}.`,
  searchText: [track.artist, track.title].filter(Boolean).join(' '),
  source: 'Playlist Intake',
  sourceId: playlist.id,
  title: [track.artist, track.title].filter(Boolean).join(' - ') || track.title,
});

export const buildPlaylistDiscoverySeeds = (playlist) =>
  playlist.tracks
    .filter((track) => track.state !== 'Rejected')
    .map((track) => buildPlaylistDiscoverySeed(playlist, track));

export const buildSlskdPlaylistPreview = (playlist) => {
  const completion = buildPlaylistCompletionSummary(playlist);
  const tracks = playlist.tracks.filter((track) => track.state === 'Matched');
  const lines = tracks.map(
    (track, index) =>
      `${index + 1}. ${[track.artist, track.title].filter(Boolean).join(' - ') || track.title}`,
  );

  return {
    completion,
    lineCount: lines.length,
    lines,
    name: playlist.name,
    networkImpact:
      'Playlist build preview only; creating it writes a playlist Collection locally and does not search Soulseek, browse peers, or download.',
    text: [`# ${playlist.name}`, ...lines].join('\n'),
  };
};

export const getDuePlaylistRefreshes = (
  playlists = readPlaylists(),
  timestamp = Date.now(),
) =>
  playlists.filter((playlist) => {
    if (!playlist.mirrorEnabled || !playlist.refreshAutomationEnabled) return false;
    if (!playlist.refreshNextRunAt) return true;

    const dueAt = Date.parse(playlist.refreshNextRunAt);
    return Number.isNaN(dueAt) || dueAt <= timestamp;
  });

export const buildPlaylistProviderRefreshContent = (result = {}) =>
  (result.suggestions || [])
    .map((suggestion) =>
      [suggestion.artist, suggestion.title || suggestion.searchText]
        .filter(Boolean)
        .join(' - '),
    )
    .filter(Boolean)
    .join('\n');

export const buildPlaylistCollectionItems = (playlist) =>
  playlist.tracks
    .filter((track) => track.state === 'Matched')
    .map((track) => ({
      contentId:
        track.contentId || `playlist-intake:${playlist.id}:${track.lineNumber}:${track.id}`,
      mediaKind: 'PlannedTrack',
    }));

export const scorePlaylistTrackCandidate = (track, candidate = {}) =>
  scoreMetadataCandidate(
    {
      artist: track.artist,
      durationMs: track.durationMs,
      title: track.title,
    },
    candidate,
  );
