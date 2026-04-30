import {
  buildWatchlistDiscoverySeed,
  buildWatchlistSchedulePreview,
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
      country: 'US',
      cooldownDays: 90,
      format: 'Vinyl',
      kind: 'Artist',
      releaseTypes: ['Album', 'Bogus'],
      schedule: 'Hourly',
      target: 'Stereolab',
    });
    saveWatchlist({
      country: 'Atlantis',
      cooldownDays: 0,
      format: 'Wax Cylinder',
      kind: 'Artist',
      releaseTypes: ['EP'],
      schedule: 'Daily',
      target: 'stereolab',
    });

    expect(getWatchlists()).toHaveLength(1);
    expect(getWatchlists()[0]).toMatchObject({
      country: 'Any',
      cooldownDays: 1,
      format: 'Any',
      kind: 'Artist',
      releaseTypes: ['EP'],
      schedule: 'Daily',
      target: 'stereolab',
    });
  });

  it('builds visible schedule previews without executing scans', () => {
    expect(
      buildWatchlistSchedulePreview({
        acquisitionProfile: 'mesh-preferred',
        cooldownDays: 3,
        schedule: 'Weekly',
      }),
    ).toMatchObject({
      cooldown: '3 days',
      enabled: true,
      label: 'Weekly schedule visible',
      profileLabel: 'Mesh Preferred',
    });

    expect(
      buildWatchlistSchedulePreview({
        acquisitionProfile: 'lossless-exact',
        cooldownDays: 1,
        schedule: 'Manual only',
      }),
    ).toMatchObject({
      cooldown: '1 day',
      enabled: false,
      label: 'Manual scans only',
      profileLabel: 'Lossless Exact',
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
      country: 'GB',
      format: 'CD',
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
    expect(seed.reason).toContain('GB country');
    expect(seed.reason).toContain('CD format');
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
