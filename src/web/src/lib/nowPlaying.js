import api from './api';

export const getNowPlaying = async () => {
  return (await api.get('/nowplaying')).data;
};

export const setNowPlaying = async ({ artist, title, album } = {}) => {
  return api.put('/nowplaying', { artist, title, album });
};

export const clearNowPlaying = async () => {
  return api.delete('/nowplaying');
};
