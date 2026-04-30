import {
  buildWatchlistDiscoverySeed,
  buildWatchlistExpansionSummary,
  buildWatchlistSchedulePreview,
  buildWatchlistSummary,
  getWatchlists,
  recordWatchlistExpansionDecision,
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
      expansionCandidates: ['Broadcast', 'Broadcast', ''],
      format: 'Vinyl',
      kind: 'Artist',
      releaseTypes: ['Album', 'Bogus'],
      schedule: 'Hourly',
      target: 'Stereolab',
    });
    saveWatchlist({
      country: 'Atlantis',
      cooldownDays: 0,
      expansionCandidates: [
        { name: 'Cavern of Anti-Matter', status: 'Approved' },
        { name: 'Unknown', status: 'Deferred' },
      ],
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
      expansionCandidates: [
        expect.objectContaining({
          name: 'Cavern of Anti-Matter',
          status: 'Approved',
        }),
        expect.objectContaining({
          name: 'Unknown',
          status: 'Pending',
        }),
      ],
      format: 'Any',
      kind: 'Artist',
      releaseTypes: ['EP'],
      schedule: 'Daily',
      target: 'stereolab',
    });
  });

  it('approves similar-artist expansion into a manual watchlist', () => {
    saveWatchlist({
      acquisitionProfile: 'rare-hunt',
      cooldownDays: 4,
      expansionCandidates: ['Broadcast', 'The Focus Group'],
      releaseTypes: ['Album', 'Single'],
      schedule: 'Weekly',
      target: 'Stereolab',
    });
    const [watch] = getWatchlists();

    recordWatchlistExpansionDecision(watch.id, 'Broadcast', 'Approved', {
      timestamp: '2026-04-30T21:25:00.000Z',
    });

    const items = getWatchlists();
    expect(items[0]).toMatchObject({
      acquisitionProfile: 'rare-hunt',
      cooldownDays: 4,
      expansionSource: 'Stereolab',
      kind: 'Artist',
      releaseTypes: ['Album', 'Single'],
      schedule: 'Manual only',
      target: 'Broadcast',
    });
    expect(
      items.find((item) => item.target === 'Stereolab').expansionCandidates,
    ).toContainEqual(
      expect.objectContaining({
        decidedAt: '2026-04-30T21:25:00.000Z',
        name: 'Broadcast',
        status: 'Approved',
      }),
    );
  });

  it('summarizes similar-artist expansion decisions', () => {
    expect(
      buildWatchlistExpansionSummary({
        expansionCandidates: [
          { name: 'Broadcast', status: 'Approved' },
          { name: 'The Focus Group', status: 'Pending' },
          { name: 'Pram', status: 'Rejected' },
        ],
      }),
    ).toMatchObject({
      Approved: 1,
      Pending: 1,
      Rejected: 1,
      total: 3,
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
