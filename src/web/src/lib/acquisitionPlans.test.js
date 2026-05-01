import {
  acquisitionPlanStorageKey,
  buildDiscoveryInboxAcquisitionPlan,
  createAcquisitionPlansFromDiscoveryInbox,
  executeAcquisitionPlanSearches,
  executeAcquisitionPlanWishlistRequests,
  getAcquisitionPlans,
} from './acquisitionPlans';

describe('acquisitionPlans', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('builds manual dry-run plans from approved discovery candidates', () => {
    const plan = buildDiscoveryInboxAcquisitionPlan({
      acquisitionProfile: 'mesh-preferred',
      evidenceKey: 'manual-search:rare-track',
      id: 'candidate-1',
      reason: 'Approved manually.',
      searchText: 'rare track',
      source: 'Discovery Inbox',
      state: 'Approved',
      title: 'rare track',
    });

    expect(plan).toEqual(
      expect.objectContaining({
        acquisitionProfile: 'mesh-preferred',
        evidenceKey: 'manual-search:rare-track',
        manualOnly: true,
        providerPriority: ['LocalLibrary', 'NativeMesh', 'MeshDht', 'Soulseek'],
        searchText: 'rare track',
        sourceId: 'candidate-1',
        state: 'Planned',
      }),
    );
    expect(plan.networkImpact).toMatch(/Dry-run plan/);
  });

  it('persists one plan per approved evidence key and candidate', () => {
    const candidate = {
      acquisitionProfile: 'rare-hunt',
      evidenceKey: 'manual-search:rare-track',
      id: 'candidate-1',
      reason: 'Approved manually.',
      searchText: 'rare track',
      source: 'Discovery Inbox',
      state: 'Approved',
      title: 'rare track',
    };

    let result = createAcquisitionPlansFromDiscoveryInbox([
      candidate,
      { ...candidate },
      { ...candidate, id: 'candidate-2', state: 'Suggested' },
    ]);

    expect(result.createdPlans).toHaveLength(1);
    expect(JSON.parse(localStorage.getItem(acquisitionPlanStorageKey))).toHaveLength(1);

    result = createAcquisitionPlansFromDiscoveryInbox([candidate]);

    expect(result.createdPlans).toHaveLength(0);
    expect(getAcquisitionPlans()).toHaveLength(1);
  });

  it('executes approved acquisition plans as bounded backend search jobs', async () => {
    const candidates = ['one', 'two', 'three', 'four'].map((title) => ({
      acquisitionProfile: 'mesh-preferred',
      evidenceKey: `manual-search:${title}`,
      id: `candidate-${title}`,
      reason: 'Approved manually.',
      searchText: title,
      source: 'Discovery Inbox',
      state: 'Approved',
      title,
    }));
    createAcquisitionPlansFromDiscoveryInbox(candidates);
    const createSearch = vi.fn().mockResolvedValue({ data: {} });

    const result = await executeAcquisitionPlanSearches([], {
      createSearch,
      maxPlans: 2,
    });

    expect(createSearch).toHaveBeenCalledTimes(2);
    expect(createSearch).toHaveBeenCalledWith(
      expect.objectContaining({
        acquisitionProfile: 'mesh-preferred',
        searchText: expect.any(String),
      }),
    );
    expect(result).toEqual(
      expect.objectContaining({
        executed: 2,
        failed: 0,
        skipped: 2,
      }),
    );
    expect(getAcquisitionPlans().filter((plan) => plan.state === 'Queued')).toHaveLength(2);
  });

  it('marks acquisition plan execution failures without retrying automatically', async () => {
    createAcquisitionPlansFromDiscoveryInbox([
      {
        acquisitionProfile: 'rare-hunt',
        evidenceKey: 'manual-search:rare-track',
        id: 'candidate-1',
        reason: 'Approved manually.',
        searchText: 'rare track',
        source: 'Discovery Inbox',
        state: 'Approved',
        title: 'rare track',
      },
    ]);
    const createSearch = vi.fn().mockRejectedValue(new Error('rate limited'));

    const result = await executeAcquisitionPlanSearches([], { createSearch });

    expect(result.failed).toBe(1);
    expect(getAcquisitionPlans()[0]).toEqual(
      expect.objectContaining({
        execution: expect.objectContaining({
          summary: 'rate limited',
        }),
        state: 'Failed',
      }),
    );
  });

  it('creates bounded Wishlist requests with auto-download disabled', async () => {
    const candidates = ['one', 'two', 'three'].map((title) => ({
      acquisitionProfile: 'mesh-preferred',
      evidenceKey: `manual-search:${title}`,
      id: `candidate-${title}`,
      reason: 'Approved manually.',
      searchText: title,
      source: 'Discovery Inbox',
      state: 'Approved',
      title,
    }));
    createAcquisitionPlansFromDiscoveryInbox(candidates);
    const createWishlist = vi
      .fn()
      .mockResolvedValueOnce({ id: 'wishlist-one' })
      .mockResolvedValueOnce({ id: 'wishlist-two' });

    const result = await executeAcquisitionPlanWishlistRequests([], {
      createWishlist,
      maxPlans: 2,
    });

    expect(createWishlist).toHaveBeenCalledTimes(2);
    expect(createWishlist).toHaveBeenCalledWith({
      autoDownload: false,
      enabled: true,
      filter: '',
      maxResults: 50,
      searchText: expect.any(String),
    });
    expect(result).toEqual(
      expect.objectContaining({
        created: 2,
        failed: 0,
        skipped: 1,
      }),
    );
    expect(getAcquisitionPlans().filter((plan) => plan.wishlistRequestId)).toHaveLength(2);
  });

  it('skips plans that already have Wishlist requests', async () => {
    createAcquisitionPlansFromDiscoveryInbox([
      {
        acquisitionProfile: 'rare-hunt',
        evidenceKey: 'manual-search:rare-track',
        id: 'candidate-1',
        reason: 'Approved manually.',
        searchText: 'rare track',
        source: 'Discovery Inbox',
        state: 'Approved',
        title: 'rare track',
      },
    ]);
    const plans = getAcquisitionPlans();
    localStorage.setItem(
      acquisitionPlanStorageKey,
      JSON.stringify([{ ...plans[0], wishlistRequestId: 'wishlist-existing' }]),
    );
    const createWishlist = vi.fn().mockResolvedValue({ id: 'wishlist-new' });

    const result = await executeAcquisitionPlanWishlistRequests([], {
      createWishlist,
    });

    expect(createWishlist).not.toHaveBeenCalled();
    expect(result).toEqual(
      expect.objectContaining({
        created: 0,
        failed: 0,
        skipped: 1,
      }),
    );
  });
});
