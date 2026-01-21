import api from './api';

export const resolveTarget = ({ releaseId, recordingId, discogsReleaseId }) => {
  return api.post('/musicbrainz/targets', {
    discogsReleaseId,
    recordingId,
    releaseId,
  });
};

export const fetchAlbumCompletion = () => {
  return api.get('/musicbrainz/albums/completion');
};
