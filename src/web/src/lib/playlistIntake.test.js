import {
  addPlaylistIntake,
  applyPlaylistIntakeRefresh,
  buildPlaylistCollectionItems,
  buildPlaylistDiscoverySeed,
  buildPlaylistDiscoverySeeds,
  buildPlaylistCompletionSummary,
  buildPlaylistIntakeSummary,
  buildPlaylistProviderRefreshContent,
  buildPlaylistRefreshDiff,
  buildSlskdPlaylistPreview,
  getDuePlaylistRefreshes,
  getPlaylistIntakes,
  previewPlaylistIntakeRefresh,
  updatePlaylistIntakeTrackState,
  updatePlaylistRefreshAutomation,
} from './playlistIntake';

describe('playlistIntake', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('imports browser-local playlist text while retaining source identity', () => {
    addPlaylistIntake({
      content: 'Stereolab - French Disko\nBroadcast - Come On Let\'s Go\nUntitled',
      mirrorEnabled: true,
      name: 'Test imports',
      source: 'local:test-imports.m3u',
    });

    const [playlist] = getPlaylistIntakes();

    expect(playlist).toMatchObject({
      mirrorEnabled: true,
      name: 'Test imports',
      provider: 'M3U',
      refreshAutomationEnabled: false,
      refreshCadence: 'Manual review',
      refreshCooldownDays: 7,
      source: 'local:test-imports.m3u',
      state: 'Staged',
    });
    expect(playlist.tracks).toHaveLength(3);
    expect(playlist.tracks[0]).toMatchObject({
      artist: 'Stereolab',
      lineNumber: 1,
      state: 'Matched',
      title: 'French Disko',
    });
    expect(playlist.tracks[2]).toMatchObject({
      artist: '',
      state: 'Unmatched',
      title: 'Untitled',
    });
  });

  it('summarizes mirrored playlists and unmatched tracks', () => {
    expect(
      buildPlaylistIntakeSummary([
        {
          mirrorEnabled: true,
          tracks: [
            { state: 'Matched' },
            { state: 'Unmatched' },
          ],
        },
        {
          mirrorEnabled: false,
          tracks: [{ state: 'Unmatched' }],
        },
      ]),
    ).toMatchObject({
      mirrored: 1,
      total: 2,
      tracks: 3,
      unmatched: 2,
    });
  });

  it('updates track review state for unmatch and rematch workflows', () => {
    addPlaylistIntake({
      content: 'Stereolab - French Disko',
      name: 'Review queue',
      source: 'local:review.txt',
    });
    const [playlist] = getPlaylistIntakes();
    const [track] = playlist.tracks;

    updatePlaylistIntakeTrackState(playlist.id, track.id, 'Unmatched');
    expect(getPlaylistIntakes()[0].tracks[0]).toMatchObject({
      state: 'Unmatched',
    });

    updatePlaylistIntakeTrackState(playlist.id, track.id, 'Matched');
    expect(getPlaylistIntakes()[0].tracks[0]).toMatchObject({
      state: 'Matched',
    });
  });

  it('previews mirrored playlist refresh diffs without mutating rows', () => {
    addPlaylistIntake({
      content: 'Stereolab - French Disko\nBroadcast - Come On Let\'s Go',
      mirrorEnabled: true,
      name: 'Mirror queue',
      source: 'local:mirror.m3u',
    });
    const [playlist] = getPlaylistIntakes();

    expect(
      buildPlaylistRefreshDiff(
        playlist,
        'Broadcast - Come On Let\'s Go\nPram - Track of the Cat',
      ),
    ).toMatchObject({
      addedCount: 1,
      changedCount: 2,
      removedCount: 1,
      totalIncoming: 2,
      unchangedCount: 1,
    });

    previewPlaylistIntakeRefresh(
      playlist.id,
      'Broadcast - Come On Let\'s Go\nPram - Track of the Cat',
    );

    const [updated] = getPlaylistIntakes();
    expect(updated.tracks).toHaveLength(2);
    expect(updated.refreshDiff).toMatchObject({
      addedCount: 1,
      changedCount: 2,
      removedCount: 1,
      unchangedCount: 1,
    });
    expect(updated.refreshPreview).toMatch(/1 added, 1 removed, 2 changed/i);
  });

  it('summarizes partial completion state', () => {
    expect(
      buildPlaylistCompletionSummary({
        tracks: [
          { state: 'Matched' },
          { state: 'Unmatched' },
          { state: 'Rejected' },
        ],
      }),
    ).toMatchObject({
      Matched: 1,
      Rejected: 1,
      Unmatched: 1,
      total: 3,
    });
  });

  it('builds review-only Discovery Inbox seeds from playlist rows', () => {
    const seed = buildPlaylistDiscoverySeed(
      {
        id: 'playlist-1',
        name: 'Road trip',
        provider: 'Local text',
      },
      {
        artist: 'Broadcast',
        lineNumber: 2,
        title: 'Come On Let\'s Go',
      },
    );

    expect(seed).toMatchObject({
      evidenceKey: 'playlist:playlist-1:2:come on let\'s go',
      searchText: 'Broadcast Come On Let\'s Go',
      source: 'Playlist Intake',
      sourceId: 'playlist-1',
      title: 'Broadcast - Come On Let\'s Go',
    });
    expect(seed.networkImpact).toMatch(/no provider fetch/i);
  });

  it('builds bulk Discovery Inbox seeds and slskdN playlist previews without mutations', () => {
    const playlist = {
      id: 'playlist-2',
      name: 'Review set',
      provider: 'Local text',
      tracks: [
        {
          artist: 'Stereolab',
          lineNumber: 1,
          state: 'Matched',
          title: 'French Disko',
        },
        {
          artist: 'Broadcast',
          lineNumber: 2,
          state: 'Unmatched',
          title: 'Come On Let\'s Go',
        },
        {
          artist: 'Rejected Artist',
          lineNumber: 3,
          state: 'Rejected',
          title: 'Skip Me',
        },
      ],
    };

    expect(buildPlaylistDiscoverySeeds(playlist)).toHaveLength(2);

    expect(buildSlskdPlaylistPreview(playlist)).toMatchObject({
      lineCount: 1,
      name: 'Review set',
      text: '# Review set\n1. Stereolab - French Disko',
    });
    expect(buildSlskdPlaylistPreview(playlist).networkImpact).toMatch(
      /writes a playlist Collection locally/i,
    );
  });

  it('enables scheduled refreshes and applies refresh rows locally', () => {
    addPlaylistIntake({
      content: 'Stereolab - French Disko',
      mirrorEnabled: true,
      name: 'Scheduled mirror',
      source: 'https://open.spotify.com/playlist/test',
    });
    let [playlist] = getPlaylistIntakes();

    updatePlaylistRefreshAutomation(playlist.id, {
      cooldownDays: 1,
      enabled: true,
    });
    [playlist] = getPlaylistIntakes();

    expect(playlist.refreshAutomationEnabled).toBe(true);
    expect(playlist.refreshNextRunAt).toBeTruthy();
    expect(
      getDuePlaylistRefreshes([playlist], Date.parse(playlist.refreshNextRunAt)),
    ).toHaveLength(1);

    applyPlaylistIntakeRefresh(
      playlist.id,
      'Stereolab - French Disko\nPram - Track of the Cat',
      { sourceLabel: 'provider refresh' },
    );

    [playlist] = getPlaylistIntakes();
    expect(playlist).toMatchObject({
      refreshAutomationEnabled: true,
      state: 'Mirrored',
    });
    expect(playlist.tracks).toHaveLength(2);
    expect(playlist.refreshPreview).toMatch(/Applied provider refresh/i);
  });

  it('builds provider refresh content and planned collection items', () => {
    expect(
      buildPlaylistProviderRefreshContent({
        suggestions: [
          { artist: 'Stereolab', title: 'French Disko' },
          { searchText: 'Untitled Provider Row' },
        ],
      }),
    ).toBe('Stereolab - French Disko\nUntitled Provider Row');

    expect(
      buildPlaylistCollectionItems({
        id: 'playlist-3',
        tracks: [
          {
            id: 'track-1',
            lineNumber: 1,
            state: 'Matched',
            title: 'French Disko',
          },
          {
            id: 'track-2',
            lineNumber: 2,
            state: 'Unmatched',
            title: 'Skip',
          },
        ],
      }),
    ).toEqual([
      {
        contentId: 'playlist-intake:playlist-3:1:track-1',
        mediaKind: 'PlannedTrack',
      },
    ]);
  });
});
