import {
  clearListeningHistory,
  getListeningHistory,
  getListeningStats,
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
      title: 'Fixture Track',
    }, '2026-04-30T20:00:00.000Z');
    recordLocalPlay({
      album: 'Fixture Album',
      artist: 'Fixture Artist',
      contentId: 'sha256:second',
      title: 'Second Track',
    }, '2026-04-30T20:02:00.000Z');

    const stats = getListeningStats();

    expect(stats.totalPlays).toBe(2);
    expect(stats.topArtists).toEqual([{ label: 'Fixture Artist', plays: 2 }]);
    expect(stats.topAlbums).toEqual([{ label: 'Fixture Album', plays: 2 }]);
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

  it('ignores corrupt stored history and can clear entries', () => {
    window.localStorage.setItem(listeningHistoryStorageKey, 'not-json');
    expect(getListeningHistory()).toEqual([]);

    recordLocalPlay({ contentId: 'sha256:first' });
    expect(getListeningHistory()).toHaveLength(1);

    clearListeningHistory();
    expect(getListeningHistory()).toEqual([]);
  });
});
