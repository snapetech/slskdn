import api from './api';

export const getSpotifyStatus = async () =>
  (await api.get('/integrations/spotify/status')).data;

export const startSpotifyAuthorization = async () =>
  (await api.post('/integrations/spotify/authorize')).data;

export const disconnectSpotify = async () => {
  await api.delete('/integrations/spotify');
};
