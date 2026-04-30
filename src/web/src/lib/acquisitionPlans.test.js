import {
  acquisitionPlanStorageKey,
  buildDiscoveryInboxAcquisitionPlan,
  createAcquisitionPlansFromDiscoveryInbox,
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
});
