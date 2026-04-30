const tokenStorageKey = 'slskdn.listenbrainz.token';

export const getListenBrainzToken = () => {
  if (typeof window === 'undefined') return '';
  return window.localStorage.getItem(tokenStorageKey) || '';
};

export const setListenBrainzToken = (token) => {
  if (typeof window === 'undefined') return;

  const normalized = token.trim();
  if (!normalized) {
    window.localStorage.removeItem(tokenStorageKey);
    return;
  }

  window.localStorage.setItem(tokenStorageKey, normalized);
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
