import {
  addWishlistItemToDiscoveryInbox,
  buildWishlistDiscoveryInboxItem,
  getWishlistEvidenceKey,
  getWishlistRequestState,
} from './acquisitionRequests';
import { discoveryInboxStorageKey } from './discoveryInbox';

describe('acquisitionRequests', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('creates stable wishlist evidence keys', () => {
    expect(
      getWishlistEvidenceKey({
        filter: 'FLAC',
        id: 'abc',
        searchText: 'Artist - Track',
      }),
    ).toBe('wishlist:abc:artist - track:flac');
  });

  it('maps wishlist defaults to unified request states', () => {
    expect(
      getWishlistRequestState({
        autoDownload: false,
        enabled: false,
        id: 'disabled',
        searchText: 'disabled',
      }).label,
    ).toBe('Disabled');
    expect(
      getWishlistRequestState({
        autoDownload: false,
        enabled: true,
        id: 'wanted',
        searchText: 'wanted',
      }).label,
    ).toBe('Wanted');
    expect(
      getWishlistRequestState({
        autoDownload: true,
        enabled: true,
        id: 'auto',
        searchText: 'auto',
      }).label,
    ).toBe('Automatic');
  });

  it('maps matching Discovery Inbox decisions over wishlist defaults', () => {
    const item = {
      autoDownload: true,
      enabled: true,
      id: 'rare',
      searchText: 'rare track',
    };

    addWishlistItemToDiscoveryInbox(item);

    expect(getWishlistRequestState(item).label).toBe('Review');
  });

  it('builds Discovery Inbox items from wishlist requests without starting work', () => {
    const item = {
      autoDownload: false,
      enabled: true,
      filter: 'flac',
      id: 'wish-1',
      searchText: 'rare album',
    };

    expect(buildWishlistDiscoveryInboxItem(item)).toEqual(
      expect.objectContaining({
        evidenceKey: 'wishlist:wish-1:rare album:flac',
        networkImpact:
          'Review only; approving here does not start peer search, browse, or download work.',
        reason: 'Saved Wishlist request with filter "flac".',
        searchText: 'rare album',
        source: 'Wishlist',
        sourceId: 'wish-1',
        title: 'rare album',
      }),
    );

    addWishlistItemToDiscoveryInbox(item);

    const persisted = JSON.parse(localStorage.getItem(discoveryInboxStorageKey));
    expect(persisted).toHaveLength(1);
    expect(persisted[0]).toEqual(
      expect.objectContaining({
        evidenceKey: 'wishlist:wish-1:rare album:flac',
        source: 'Wishlist',
        sourceId: 'wish-1',
      }),
    );
  });
});
