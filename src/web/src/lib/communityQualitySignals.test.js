import {
  clearCommunityQualitySignalsForUser,
  communityQualitySignalStorageKey,
  getCommunityQualityLabel,
  getCommunityQualitySignals,
  getCommunityQualitySummary,
  recordCommunityQualitySignal,
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
});
