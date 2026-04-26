import api from './api';

export const getAll = async () => {
  return (await api.get('/wishlist')).data;
};

export const get = async (id) => {
  return (await api.get(`/wishlist/${id}`)).data;
};

export const create = async ({
  searchText,
  filter,
  enabled,
  autoDownload,
  maxResults,
}) => {
  return (
    await api.post('/wishlist', {
      autoDownload,
      enabled,
      filter,
      maxResults,
      searchText,
    })
  ).data;
};

export const update = async (
  id,
  { searchText, filter, enabled, autoDownload, maxResults },
) => {
  return (
    await api.put(`/wishlist/${id}`, {
      autoDownload,
      enabled,
      filter,
      maxResults,
      searchText,
    })
  ).data;
};

export const remove = async (id) => {
  await api.delete(`/wishlist/${id}`);
};

export const runSearch = async (id) => {
  return (await api.post(`/wishlist/${id}/search`)).data;
};

export const importCsv = async ({
  csvText,
  filter,
  enabled,
  autoDownload,
  maxResults,
  includeAlbum,
}) => {
  return (
    await api.post('/wishlist/import/csv', {
      autoDownload,
      csvText,
      enabled,
      filter,
      includeAlbum,
      maxResults,
    })
  ).data;
};
