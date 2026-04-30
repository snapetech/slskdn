export const mediaServerAdapters = [
  {
    capabilities: ['Library scan', 'Playlist sync', 'Play history import', 'Rating sync'],
    id: 'plex',
    label: 'Plex',
    requiresToken: true,
  },
  {
    capabilities: ['Library scan', 'Playlist sync', 'Play history import', 'User mapping'],
    id: 'jellyfin',
    label: 'Jellyfin / Emby',
    requiresToken: true,
  },
  {
    capabilities: ['Library scan', 'Playlist sync', 'Play history import'],
    id: 'navidrome',
    label: 'Navidrome',
    requiresToken: true,
  },
];

const normalizePath = (value = '') =>
  value
    .trim()
    .replaceAll('\\', '/')
    .replace(/\/+/gu, '/')
    .replace(/\/$/u, '');

export const buildMediaServerPathDiagnostic = ({
  localPath = '',
  serverPath = '',
  remotePathFrom = '',
  remotePathTo = '',
} = {}) => {
  const normalizedLocal = normalizePath(localPath);
  const normalizedServer = normalizePath(serverPath);
  const normalizedFrom = normalizePath(remotePathFrom);
  const normalizedTo = normalizePath(remotePathTo);

  if (!normalizedLocal || !normalizedServer) {
    return {
      color: 'grey',
      message: 'Enter both paths to check whether slskdN and the media server agree.',
      status: 'Incomplete',
    };
  }

  if (normalizedLocal === normalizedServer) {
    return {
      color: 'green',
      message: 'Paths match exactly. A media-server scan can reference the same completed files.',
      status: 'Aligned',
    };
  }

  if (
    normalizedFrom &&
    normalizedTo &&
    normalizedLocal.startsWith(normalizedFrom)
  ) {
    const mapped = `${normalizedTo}${normalizedLocal.slice(normalizedFrom.length)}`;
    return {
      color: mapped === normalizedServer ? 'green' : 'yellow',
      mappedPath: mapped,
      message:
        mapped === normalizedServer
          ? 'Remote path mapping translates the slskdN path to the media-server path.'
          : 'Remote path mapping applies, but the translated path does not match the media-server path.',
      status: mapped === normalizedServer ? 'Mapped' : 'Mapping Mismatch',
    };
  }

  return {
    color: 'orange',
    message: 'Paths differ and no matching remote path map was provided.',
    status: 'Needs Mapping',
  };
};
