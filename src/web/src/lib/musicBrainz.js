import api from './api';

export const resolveTarget = ({ releaseId, recordingId, discogsReleaseId }) => {
  return api.post('/musicbrainz/targets', { releaseId, recordingId, discogsReleaseId });
};

export const fetchAlbumCompletion = () => {
  return api.get('/musicbrainz/albums/completion');
};

















