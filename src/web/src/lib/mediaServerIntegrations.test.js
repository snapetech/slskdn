import {
  buildMediaServerPathDiagnostic,
  mediaServerAdapters,
} from './mediaServerIntegrations';

describe('mediaServerIntegrations', () => {
  it('lists common media-server adapters without making them required', () => {
    expect(mediaServerAdapters.map((adapter) => adapter.id)).toEqual([
      'plex',
      'jellyfin',
      'navidrome',
    ]);
    expect(mediaServerAdapters.every((adapter) => adapter.requiresToken)).toBe(true);
  });

  it('detects matching report paths', () => {
    expect(
      buildMediaServerPathDiagnostic({
        localPath: '/media/music/Album/track.flac',
        serverPath: '/media/music/Album/track.flac',
      }),
    ).toEqual(
      expect.objectContaining({
        color: 'green',
        status: 'Aligned',
      }),
    );
  });

  it('detects valid remote path mappings', () => {
    expect(
      buildMediaServerPathDiagnostic({
        localPath: '/downloads/complete/Album/track.flac',
        remotePathFrom: '/downloads/complete',
        remotePathTo: '/library/music',
        serverPath: '/library/music/Album/track.flac',
      }),
    ).toEqual(
      expect.objectContaining({
        mappedPath: '/library/music/Album/track.flac',
        status: 'Mapped',
      }),
    );
  });

  it('warns when paths need mapping', () => {
    expect(
      buildMediaServerPathDiagnostic({
        localPath: '/downloads/complete/Album/track.flac',
        serverPath: '/library/music/Album/track.flac',
      }),
    ).toEqual(
      expect.objectContaining({
        color: 'orange',
        status: 'Needs Mapping',
      }),
    );
  });
});
