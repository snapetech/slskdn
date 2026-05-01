import {
  clearCommunityQualitySignalsForUser,
  clearCommunityQualityOverride,
  communityQualityOverrideStorageKey,
  communityQualitySignalStorageKey,
  getCommunityQualityLabel,
  getCommunityQualityOverrides,
  getCommunityQualitySignals,
  getCommunityQualitySummary,
  recordCommunityQualitySignal,
  setCommunityQualityOverride,
} from './communityQualitySignals';

describe('communityQualitySignals', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('records local-only peer quality signals', () => {
    recordCommunityQualitySignal({
      reason: 'Reported suspicious filename from search review.',
      type: 'suspicious-candidate',
      username: 'peer-a',
    });

    const persisted = JSON.parse(
      localStorage.getItem(communityQualitySignalStorageKey),
    );

    expect(persisted).toHaveLength(1);
    expect(persisted[0]).toEqual(
      expect.objectContaining({
        category: 'negative',
        reason: 'Reported suspicious filename from search review.',
        source: 'local-review',
        type: 'suspicious-candidate',
        username: 'peer-a',
      }),
    );
    expect(getCommunityQualitySignals()).toHaveLength(1);
  });

  it('summarizes positive and negative signals without global punishment', () => {
    recordCommunityQualitySignal({
      type: 'served-verified-content',
      username: 'peer-a',
    });
    recordCommunityQualitySignal({
      type: 'suspicious-candidate',
      username: 'peer-a',
    });
    recordCommunityQualitySignal({
      type: 'suspicious-candidate',
      username: 'peer-a',
    });

    const summary = getCommunityQualitySummary('peer-a');

    expect(summary.positive).toBe(1);
    expect(summary.negative).toBe(2);
    expect(summary.score).toBe(-8);
    expect(getCommunityQualityLabel(summary)).toEqual(
      expect.objectContaining({
        color: 'orange',
        text: 'Local caution',
      }),
    );
  });

  it('clears signals for one peer without removing other peers', () => {
    recordCommunityQualitySignal({
      type: 'suspicious-candidate',
      username: 'peer-a',
    });
    recordCommunityQualitySignal({
      type: 'served-verified-content',
      username: 'peer-b',
    });

    clearCommunityQualitySignalsForUser('peer-a');

    expect(getCommunityQualitySummary('peer-a').signals).toHaveLength(0);
    expect(getCommunityQualitySummary('peer-b').signals).toHaveLength(1);
  });

  it('stores local notes and ignores signals for one peer without deleting evidence', () => {
    recordCommunityQualitySignal({
      type: 'suspicious-candidate',
      username: 'fixture-peer',
    });

    setCommunityQualityOverride('fixture-peer', {
      mode: 'ignore',
      note: 'Known household peer; ignore old noisy review signal.',
    });

    const persisted = JSON.parse(
      localStorage.getItem(communityQualityOverrideStorageKey),
    );
    expect(persisted['fixture-peer']).toEqual(
      expect.objectContaining({
        mode: 'ignore',
        note: 'Known household peer; ignore old noisy review signal.',
      }),
    );

    const summary = getCommunityQualitySummary('fixture-peer');
    expect(summary.rawScore).toBe(-6);
    expect(summary.score).toBe(0);
    expect(summary.signals).toHaveLength(1);
    expect(getCommunityQualityLabel(summary)).toEqual(
      expect.objectContaining({
        text: 'Signals ignored',
      }),
    );

    clearCommunityQualityOverride('fixture-peer');
    expect(getCommunityQualityOverrides()['fixture-peer']).toBeUndefined();
  });

  it('supports explicit local trust and caution overrides for ranking input', () => {
    setCommunityQualityOverride('fixture-peer', {
      mode: 'trust',
      note: 'Verified private source.',
    });

    expect(getCommunityQualitySummary('fixture-peer')).toEqual(
      expect.objectContaining({
        override: expect.objectContaining({
          mode: 'trust',
        }),
        rawScore: 0,
        score: 8,
      }),
    );

    setCommunityQualityOverride('fixture-peer', {
      mode: 'caution',
      note: 'Manual caution until review clears.',
    });

    expect(getCommunityQualitySummary('fixture-peer')).toEqual(
      expect.objectContaining({
        rawScore: 0,
        score: -6,
      }),
    );
  });
});
