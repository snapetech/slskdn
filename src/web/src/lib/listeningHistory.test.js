import {
  buildListeningDiscoverySeeds,
  clearListeningHistory,
  exportListeningHistoryCsv,
  exportListeningHistoryJson,
  getListeningHistory,
  getListeningRecommendationQueries,
  getListeningRecommendationSeeds,
  getListeningStats,
  importListeningHistory,
  listeningHistoryStorageKey,
  recordLocalPlay,
} from './listeningHistory';

describe('listeningHistory', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('records local plays and summarizes listening stats', () => {
    recordLocalPlay({
      album: 'Fixture Album',
      artist: 'Fixture Artist',
      contentId: 'sha256:first',
      genre: 'Fixture Genre',
      title: 'Fixture Track',
    }, '2026-04-30T20:00:00.000Z');
    recordLocalPlay({
      album: 'Fixture Album',
      artist: 'Fixture Artist',
      contentId: 'sha256:second',
      tags: ['Fixture Genre', 'Other Tag'],
      title: 'Second Track',
    }, '2026-04-30T20:02:00.000Z');

    const stats = getListeningStats();

    expect(stats.totalPlays).toBe(2);
    expect(stats.topArtists).toEqual([{ label: 'Fixture Artist', plays: 2 }]);
    expect(stats.topAlbums).toEqual([{ label: 'Fixture Album', plays: 2 }]);
    expect(stats.topGenres[0]).toEqual({ label: 'Fixture Genre', plays: 2 });
    expect(stats.topTracks[0]).toEqual({ label: 'Fixture Track', plays: 1 });
    expect(stats.recent[0].title).toBe('Second Track');
  });

  it('filters stats by time range and finds forgotten favorites', () => {
    const olderFavorite = {
      album: 'Fixture Album',
      artist: 'Fixture Artist',
      contentId: 'sha256:old',
      title: 'Older Favorite',
    };

    recordLocalPlay(olderFavorite, '2026-03-01T20:00:00.000Z');
    recordLocalPlay(olderFavorite, '2026-03-01T20:01:00.000Z');
    recordLocalPlay({
      artist: 'New Artist',
      contentId: 'sha256:new',
      title: 'New Track',
    }, '2026-04-29T20:00:00.000Z');

    const stats = getListeningStats({
      now: '2026-04-30T20:00:00.000Z',
      rangeDays: 7,
    });

    expect(stats.totalPlays).toBe(1);
    expect(stats.topArtists).toEqual([{ label: 'New Artist', plays: 1 }]);
    expect(stats.forgottenFavorites).toEqual([
      {
        album: 'Fixture Album',
        artist: 'Fixture Artist',
        lastPlayedAt: '2026-03-01T20:01:00.000Z',
        plays: 2,
        title: 'Older Favorite',
      },
    ]);
  });

  it('builds explicit recommendation search seeds from local stats', () => {
    const olderFavorite = {
      album: 'Fixture Album',
      artist: 'Fixture Artist',
      contentId: 'sha256:old',
      genre: 'Fixture Genre',
      title: 'Older Favorite',
    };

    recordLocalPlay(olderFavorite, '2026-03-01T20:00:00.000Z');
    recordLocalPlay(olderFavorite, '2026-03-01T20:01:00.000Z');
    recordLocalPlay({
      artist: 'Second Fixture Artist',
      contentId: 'sha256:new',
      tags: ['Fixture Genre'],
      title: 'New Track',
    }, '2026-04-29T20:00:00.000Z');

    const stats = getListeningStats({
      now: '2026-04-30T20:00:00.000Z',
      rangeDays: 7,
    });
    const seeds = getListeningRecommendationSeeds(stats);

    expect(seeds[0]).toEqual({
      basis: '2 older plays',
      label: 'Older Favorite',
      query: 'Fixture Artist Older Favorite',
      type: 'Forgotten favorite',
    });
    expect(seeds).toEqual(expect.arrayContaining([
      {
        basis: '1 local plays',
        label: 'Second Fixture Artist',
        query: 'Second Fixture Artist',
        type: 'Artist seed',
      },
      {
        basis: '1 tagged plays',
        label: 'Fixture Genre',
        query: 'Fixture Genre',
        type: 'Genre seed',
      },
      {
        basis: '1 local plays',
        label: 'New Track',
        query: 'New Track',
        type: 'Track seed',
      },
    ]));

    const inboxSeeds = buildListeningDiscoverySeeds(stats, {
      acquisitionProfile: 'mesh-preferred',
    });
    expect(inboxSeeds[0]).toEqual(
      expect.objectContaining({
        acquisitionProfile: 'mesh-preferred',
        evidenceKey: 'listening:forgotten favorite:fixture artist older favorite',
        networkImpact: expect.stringContaining('approval and explicit acquisition execution'),
        source: 'Listening Stats',
      }),
    );
    expect(getListeningRecommendationQueries(stats, { limit: 2 })).toEqual([
      'Fixture Artist Older Favorite',
      'Second Fixture Artist',
    ]);
  });

  it('deduplicates immediate duplicate plays for the same track', () => {
    const track = {
      artist: 'Fixture Artist',
      contentId: 'sha256:first',
      title: 'Fixture Track',
    };

    recordLocalPlay(track, '2026-04-30T20:00:00.000Z');
    recordLocalPlay(track, '2026-04-30T20:00:15.000Z');

    expect(getListeningHistory()).toHaveLength(1);
  });

  it('imports media-server play history from CSV and skips duplicates', () => {
    const csv = [
      'playedAt,artist,album,title,genre',
      '2026-04-30T20:00:00Z,Fixture Artist,Fixture Album,Fixture Track,Fixture Genre',
      '2026-04-30T20:00:00Z,Fixture Artist,Fixture Album,Fixture Track,Fixture Genre',
      '2026-04-29T20:00:00Z,Second Artist,Second Album,Second Track,Second Genre',
    ].join('\n');

    const result = importListeningHistory(csv, 'fixture-media-server');

    expect(result.imported).toBe(2);
    expect(result.skipped).toBe(1);
    expect(getListeningHistory()).toHaveLength(2);
    expect(getListeningHistory()[0]).toMatchObject({
      artist: 'Fixture Artist',
      genres: ['Fixture Genre'],
      playedAt: '2026-04-30T20:00:00.000Z',
      source: 'fixture-media-server',
      title: 'Fixture Track',
    });
  });

  it('imports media-server play history from JSON and exports review files', () => {
    const json = JSON.stringify({
      history: [
        {
          album: 'Fixture Album',
          artistName: 'Fixture Artist',
          genre: 'Fixture Genre',
          played_at: '2026-04-30T20:00:00Z',
          trackTitle: 'Fixture Track',
        },
      ],
    });

    const result = importListeningHistory(json);

    expect(result.imported).toBe(1);
    expect(exportListeningHistoryJson()).toContain('Fixture Track');
    expect(exportListeningHistoryCsv()).toContain('playedAt,artist,album,title');
    expect(exportListeningHistoryCsv()).toContain('Fixture Artist');
  });

  it('ignores corrupt stored history and can clear entries', () => {
    window.localStorage.setItem(listeningHistoryStorageKey, 'not-json');
    expect(getListeningHistory()).toEqual([]);

    recordLocalPlay({ contentId: 'sha256:first' });
    expect(getListeningHistory()).toHaveLength(1);

    clearListeningHistory();
    expect(getListeningHistory()).toEqual([]);
  });
});
