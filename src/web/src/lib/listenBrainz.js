import { getLocalStorageItem, removeLocalStorageItem, setLocalStorageItem } from './storage';

const tokenStorageKey = 'slskdn.listenbrainz.token';

export const getListenBrainzToken = () =>
  getLocalStorageItem(tokenStorageKey, '');

export const setListenBrainzToken = (token) => {
  const normalized = token.trim();
  if (!normalized) {
    removeLocalStorageItem(tokenStorageKey);
    return;
  }

  setLocalStorageItem(tokenStorageKey, normalized);
};

export const submitListen = async (listenType, track) => {
  const token = getListenBrainzToken();
  if (!token || !track?.artist || !track?.title) return false;

  const payload = {
    listen_type: listenType,
    payload: [
      {
        listened_at:
          listenType === 'single' ? Math.floor(Date.now() / 1000) : undefined,
        track_metadata: {
          additional_info: {
            media_player: 'slskdN Web Player',
            release_name: track.album || undefined,
          },
          artist_name: track.artist,
          release_name: track.album || undefined,
          track_name: track.title,
        },
      },
    ],
  };

  const response = await fetch('https://api.listenbrainz.org/1/submit-listens', {
    body: JSON.stringify(payload),
    headers: {
      Authorization: `Token ${token}`,
      'Content-Type': 'application/json',
    },
    method: 'POST',
  });
  return response.ok;
};

export const submitListeningHistory = async (entries = [], { limit = 10 } = {}) => {
  const token = getListenBrainzToken();
  if (!token) {
    return {
      submitted: 0,
      skipped: entries.length,
    };
  }

  const payloadEntries = entries
    .filter((entry) => entry?.artist && entry?.title)
    .filter((entry) => Number.isFinite(Date.parse(entry.playedAt || '')))
    .slice(0, limit)
    .map((entry) => ({
      listened_at: Math.floor(Date.parse(entry.playedAt) / 1000),
      track_metadata: {
        additional_info: {
          media_player: 'slskdN Web Player',
          release_name: entry.album || undefined,
        },
        artist_name: entry.artist,
        release_name: entry.album || undefined,
        track_name: entry.title,
      },
    }));

  if (payloadEntries.length === 0) {
    return {
      submitted: 0,
      skipped: entries.length,
    };
  }

  const response = await fetch('https://api.listenbrainz.org/1/submit-listens', {
    body: JSON.stringify({
      listen_type: 'import',
      payload: payloadEntries,
    }),
    headers: {
      Authorization: `Token ${token}`,
      'Content-Type': 'application/json',
    },
    method: 'POST',
  });

  return {
    submitted: response.ok ? payloadEntries.length : 0,
    skipped: entries.length - payloadEntries.length,
  };
};
