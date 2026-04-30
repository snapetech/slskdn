import {
  buildWatchlistDiscoverySeed,
  buildWatchlistSummary,
  getWatchlists,
  recordWatchlistManualScan,
  saveWatchlist,
} from './watchlists';

describe('watchlists', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('saves normalized watchlist targets without duplicating kind and target', () => {
    saveWatchlist({
      kind: 'Artist',
      releaseTypes: ['Album', 'Bogus'],
      target: 'Stereolab',
    });
    saveWatchlist({
      kind: 'Artist',
      releaseTypes: ['EP'],
      target: 'stereolab',
    });

    expect(getWatchlists()).toHaveLength(1);
    expect(getWatchlists()[0]).toMatchObject({
      kind: 'Artist',
      releaseTypes: ['EP'],
      target: 'stereolab',
    });
  });

  it('records manual scan previews without provider or peer activity', () => {
    saveWatchlist({ target: 'Broadcast' });
    const [watch] = getWatchlists();

    recordWatchlistManualScan(watch.id, {
      timestamp: '2026-04-30T20:55:53.000Z',
    });

    expect(getWatchlists()[0]).toMatchObject({
      lastScannedAt: '2026-04-30T20:55:53.000Z',
      lastScanPreview:
        'Manual scan preview only; no provider lookup or peer search was started.',
    });
  });

  it('builds a Discovery Inbox seed from a watchlist target', () => {
    const seed = buildWatchlistDiscoverySeed({
      acquisitionProfile: 'rare-hunt',
      id: 'watch-1',
      kind: 'Label',
      releaseTypes: ['Album', 'Single'],
      target: 'Ghost Box',
    });

    expect(seed).toMatchObject({
      acquisitionProfile: 'rare-hunt',
      evidenceKey: 'watchlist:label:ghost box',
      searchText: 'Ghost Box',
      source: 'Watchlist',
      sourceId: 'watch-1',
    });
    expect(seed.networkImpact).toMatch(/no provider lookup/i);
  });

  it('summarizes watchlist kinds and scheduled entries', () => {
    expect(
      buildWatchlistSummary([
        { kind: 'Artist', schedule: 'Manual only' },
        { kind: 'Label', schedule: 'Weekly' },
      ]),
    ).toMatchObject({
      Artist: 1,
      Label: 1,
      scheduled: 1,
      total: 2,
    });
  });
});
