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

export const subscribeArtistReleaseRadar = ({
  artistId,
  artistName,
  enabled = true,
  mutedReleaseGroupIds = [],
  scope = 'trusted',
}) =>
  api.post('/musicbrainz/release-radar/subscriptions', {
    artistId,
    artistName,
    enabled,
    mutedReleaseGroupIds,
    scope,
  });

export const fetchArtistReleaseRadarSubscriptions = () =>
  api.get('/musicbrainz/release-radar/subscriptions');

export const fetchArtistReleaseRadarNotifications = ({ unreadOnly = false } = {}) =>
  api.get('/musicbrainz/release-radar/notifications', {
    params: { unreadOnly },
  });

export const routeArtistReleaseRadarNotification = ({
  channelId,
  notificationId,
  podId,
  senderPeerId,
  targetPeerIds = [],
}) =>
  api.post(
    `/musicbrainz/release-radar/notifications/${encodeURIComponent(notificationId)}/routes`,
    {
      channelId,
      podId,
      senderPeerId,
      targetPeerIds,
    },
  );
