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
