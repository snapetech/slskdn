import api from './api';

export const previewSourceFeedImport = async ({
  sourceText,
  sourceKind = 'auto',
  providerAccessToken = '',
  includeAlbum = false,
  fetchProviderUrls = true,
  limit = 500,
}) => {
  return (
    await api.post('/source-feed-imports/preview', {
      fetchProviderUrls,
      includeAlbum,
      limit,
      providerAccessToken,
      sourceKind,
      sourceText,
    })
  ).data;
};
