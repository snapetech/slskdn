import {
  addDiscoveryInboxItem,
  bulkUpdateDiscoveryInboxItems,
  discoveryInboxStorageKey,
  getDiscoveryInboxItems,
  updateDiscoveryInboxItemState,
} from './discoveryInbox';

describe('discoveryInbox', () => {
  const makeStorage = () => {
    const store = new Map();
    return {
      getItem: (key, fallback = null) => store.get(key) ?? fallback,
      setItem: (key, value) => {
        store.set(key, value);
        return true;
      },
    };
  };

  it('persists normalized discovery candidates', () => {
    const storage = makeStorage();

    const item = addDiscoveryInboxItem(
      {
        evidenceKey: 'manual-search:test',
        searchText: 'test artist',
        source: 'Search',
      },
      storage,
    );

    expect(item).toEqual(
      expect.objectContaining({
        acquisitionProfile: 'lossless-exact',
        evidenceKey: 'manual-search:test',
        reason: 'Manual discovery suggestion.',
        searchText: 'test artist',
        source: 'Search',
        sourceId: '',
        state: 'Suggested',
        title: 'test artist',
      }),
    );
    expect(JSON.parse(storage.getItem(discoveryInboxStorageKey))).toHaveLength(1);
  });

  it('deduplicates evidence including rejected decisions', () => {
    const storage = makeStorage();
    const first = addDiscoveryInboxItem(
      {
        evidenceKey: 'manual-search:test',
        searchText: 'test artist',
        source: 'Search',
      },
      storage,
    );

    const duplicate = addDiscoveryInboxItem(
      {
        evidenceKey: 'manual-search:test',
        searchText: 'test artist',
        source: 'Search',
      },
      storage,
    );

    expect(duplicate.id).toBe(first.id);
    expect(getDiscoveryInboxItems(storage.getItem)).toHaveLength(1);

    updateDiscoveryInboxItemState(first.id, 'Rejected', storage);
    const afterReject = addDiscoveryInboxItem(
      {
        evidenceKey: 'manual-search:test',
        searchText: 'test artist',
        source: 'Search',
      },
      storage,
    );

    expect(afterReject.id).toBe(first.id);
    expect(getDiscoveryInboxItems(storage.getItem)).toHaveLength(1);
  });

  it('updates individual and bulk item states', () => {
    const storage = makeStorage();
    const first = addDiscoveryInboxItem(
      {
        evidenceKey: 'manual-search:first',
        searchText: 'first',
        source: 'Search',
      },
      storage,
    );
    const second = addDiscoveryInboxItem(
      {
        evidenceKey: 'manual-search:second',
        searchText: 'second',
        source: 'Search',
      },
      storage,
    );

    let items = updateDiscoveryInboxItemState(first.id, 'Approved', storage);
    expect(items.find((item) => item.id === first.id).state).toBe('Approved');

    items = bulkUpdateDiscoveryInboxItems([first.id, second.id], 'Snoozed', storage);
    expect(items.map((item) => item.state)).toEqual(['Snoozed', 'Snoozed']);
  });
});
