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

export const fetchDiscographyCoverage = ({
  artistId,
  forceRefresh = false,
  profile = 'CoreDiscography',
}) => {
  return api.get(`/musicbrainz/artist/${encodeURIComponent(artistId)}/discography-coverage`, {
    params: {
      forceRefresh,
      profile,
    },
  });
};

export const promoteDiscographyCoverageToWishlist = ({
  artistId,
  filter = 'flac',
  maxResults = 100,
  profile = 'CoreDiscography',
}) => {
  return api.post(
    `/musicbrainz/artist/${encodeURIComponent(artistId)}/discography-coverage/wishlist`,
    {
      filter,
      maxResults,
      profile,
    },
  );
};
