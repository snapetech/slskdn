import {
  buildDiscoveryInboxReviewSummary,
  classifyDiscoveryInboxImpact,
} from './discoveryInboxReview';

describe('discoveryInboxReview', () => {
  it('classifies local/manual review items as low impact', () => {
    expect(
      classifyDiscoveryInboxImpact({
        networkImpact: 'Manual review only; no network request until approved.',
      }),
    ).toMatchObject({
      label: 'Local/manual',
      level: 'local-manual',
    });
  });

  it('classifies provider lookup items separately from direct network risk', () => {
    expect(
      classifyDiscoveryInboxImpact({
        networkImpact: 'Release radar provider lookup required before planning.',
        source: 'Release Radar',
      }),
    ).toMatchObject({
      label: 'Provider review',
      level: 'provider-review',
    });
  });

  it('summarizes review readiness for batch approval', () => {
    const summary = buildDiscoveryInboxReviewSummary([
      { networkImpact: 'Manual review only; no network request.' },
      { networkImpact: 'Provider metadata lookup required.' },
      { networkImpact: 'Automatic download would contact peers.' },
    ]);

    expect(summary).toMatchObject({
      'local-manual': 1,
      'network-risk': 1,
      'provider-review': 1,
      canBulkApproveSafely: false,
      total: 3,
    });
  });
});
