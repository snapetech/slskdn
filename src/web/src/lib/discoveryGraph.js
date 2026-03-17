import api from './api';

export const buildDiscoveryGraph = async (request) => {
  const response = await api.post('/discovery-graph', request);
  return response.data;
};

export const toQueryString = (request = {}) => {
  const parameters = new URLSearchParams();

  Object.entries(request).forEach(([key, value]) => {
    if (value !== undefined && value !== null && `${value}`.trim() !== '') {
      parameters.set(key, value);
    }
  });

  return parameters.toString();
};

export const fromQueryString = (search = '') => {
  const parameters = new URLSearchParams(search.startsWith('?') ? search : `?${search}`);
  return {
    album: parameters.get('album') || undefined,
    artist: parameters.get('artist') || undefined,
    artistId: parameters.get('artistId') || undefined,
    compareLabel: parameters.get('compareLabel') || undefined,
    compareNodeId: parameters.get('compareNodeId') || undefined,
    recordingId: parameters.get('recordingId') || undefined,
    releaseId: parameters.get('releaseId') || undefined,
    scope: parameters.get('scope') || 'songid_run',
    songIdRunId: parameters.get('songIdRunId') || undefined,
    title: parameters.get('title') || undefined,
  };
};
