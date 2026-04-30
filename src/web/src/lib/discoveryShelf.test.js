import {
  clearDiscoveryShelf,
  discoveryShelfStorageKey,
  getDiscoveryShelfAction,
  getDiscoveryShelfSummary,
  removeDiscoveryShelfItem,
  upsertDiscoveryShelfItem,
} from './discoveryShelf';

describe('discoveryShelf', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('stores local shelf actions from player ratings', () => {
    upsertDiscoveryShelfItem({
      artist: 'Fixture Artist',
      contentId: 'sha256:fixture',
      sourceProviders: ['local', 'mesh'],
      title: 'Fixture Track',
    }, 5, '2026-04-30T00:00:00.000Z');

    const [item] = JSON.parse(localStorage.getItem(discoveryShelfStorageKey));

    expect(item).toMatchObject({
      action: 'promote-preview',
      artist: 'Fixture Artist',
      key: 'sha256:fixture',
      rating: 5,
      reviewedAt: '2026-04-30T00:00:00.000Z',
      title: 'Fixture Track',
    });
  });

  it('summarizes expiry-watch actions using the same key that unrated items store', () => {
    expect(getDiscoveryShelfAction(0)).toBe('expiry-watch');

    upsertDiscoveryShelfItem({
      contentId: 'sha256:unrated',
      title: 'Unrated Track',
    }, 0);

    expect(getDiscoveryShelfSummary()).toMatchObject({
      'expiry-watch': 1,
      total: 1,
    });
  });

  it('removes and clears shelf items', () => {
    upsertDiscoveryShelfItem({
      contentId: 'sha256:remove',
      title: 'Remove Track',
    }, 1);

    removeDiscoveryShelfItem('sha256:remove');
    expect(getDiscoveryShelfSummary().total).toBe(0);

    upsertDiscoveryShelfItem({
      contentId: 'sha256:clear',
      title: 'Clear Track',
    }, 3);
    clearDiscoveryShelf();

    expect(getDiscoveryShelfSummary().total).toBe(0);
  });
});
