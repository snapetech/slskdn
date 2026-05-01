import { getAcquisitionProfile } from './acquisitionProfiles';
import { getLocalStorageItem, setLocalStorageItem } from './storage';
import { v4 as uuidv4 } from 'uuid';

export const acquisitionPlanStorageKey = 'slskdn.acquisitionPlans.items';

export const acquisitionPlanStates = [
  'Planned',
  'Ready',
  'Queued',
  'Executing',
  'Completed',
  'Rejected',
  'Failed',
];

export const profileProviderPriority = {
  'album-complete': ['LocalLibrary', 'Soulseek', 'NativeMesh', 'MeshDht'],
  'conservative-network': ['LocalLibrary', 'Soulseek'],
  'fast-good-enough': ['LocalLibrary', 'Soulseek'],
  'lossless-exact': ['LocalLibrary', 'Soulseek', 'NativeMesh', 'MeshDht'],
  'mesh-preferred': ['LocalLibrary', 'NativeMesh', 'MeshDht', 'Soulseek'],
  'metadata-strict': ['LocalLibrary', 'Soulseek', 'NativeMesh', 'MeshDht'],
  'rare-hunt': ['LocalLibrary', 'Soulseek', 'NativeMesh', 'MeshDht', 'Http', 'WebDav', 'S3'],
};

const now = () => new Date().toISOString();

const normalizeState = (state) =>
  acquisitionPlanStates.includes(state) ? state : 'Planned';

const normalizeText = (value) => `${value || ''}`.trim();

const normalizePlan = (plan) => {
  const timestamp = now();
  const profile = getAcquisitionProfile(plan.acquisitionProfile);

  return {
    acquisitionProfile: profile.id,
    createdAt: plan.createdAt || timestamp,
    evidenceKey: normalizeText(plan.evidenceKey),
    execution: plan.execution || null,
    id: plan.id || uuidv4(),
    manualOnly: plan.manualOnly !== false,
    networkImpact:
      plan.networkImpact ||
      'Dry-run plan only; no peer search, browse, download, DHT lookup, or remote request has started.',
    providerPriority:
      plan.providerPriority ||
      profileProviderPriority[profile.id] ||
      profileProviderPriority['lossless-exact'],
    reason: plan.reason || 'Approved discovery candidate.',
    queuedSearchId: plan.queuedSearchId || '',
    searchText: normalizeText(plan.searchText || plan.title),
    source: plan.source || 'Discovery Inbox',
    sourceId: plan.sourceId || '',
    state: normalizeState(plan.state),
    title: normalizeText(plan.title || plan.searchText || 'Untitled acquisition plan'),
    updatedAt: plan.updatedAt || timestamp,
    wishlistRequestId: plan.wishlistRequestId || '',
  };
};

export const getAcquisitionPlans = (getItem = getLocalStorageItem) => {
  try {
    const parsed = JSON.parse(getItem(acquisitionPlanStorageKey, '[]'));
    return Array.isArray(parsed) ? parsed.map(normalizePlan) : [];
  } catch {
    return [];
  }
};

export const saveAcquisitionPlans = (
  plans,
  setItem = setLocalStorageItem,
) => {
  const normalized = plans.map(normalizePlan);
  setItem(acquisitionPlanStorageKey, JSON.stringify(normalized));
  return normalized;
};

export const buildDiscoveryInboxAcquisitionPlan = (candidate) =>
  normalizePlan({
    acquisitionProfile: candidate.acquisitionProfile,
    evidenceKey: candidate.evidenceKey,
    manualOnly: true,
    networkImpact:
      'Dry-run plan created from an approved Discovery Inbox candidate. Review and explicit execution are still required before any network activity.',
    reason: candidate.reason,
    searchText: candidate.searchText,
    source: candidate.source,
    sourceId: candidate.id,
    title: candidate.title,
  });

export const createAcquisitionPlansFromDiscoveryInbox = (
  candidates,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const approvedCandidates = candidates.filter((candidate) => candidate.state === 'Approved');
  const plans = getAcquisitionPlans(getItem);
  const existingKeys = new Set(
    plans.map((plan) => `${plan.evidenceKey}:${plan.sourceId}`),
  );
  const createdPlans = approvedCandidates
    .map(buildDiscoveryInboxAcquisitionPlan)
    .filter((plan) => {
      const key = `${plan.evidenceKey}:${plan.sourceId}`;
      if (existingKeys.has(key)) {
        return false;
      }

      existingKeys.add(key);
      return true;
    });

  return {
    createdPlans,
    plans: saveAcquisitionPlans([...createdPlans, ...plans], setItem),
  };
};

const canExecutePlan = (plan) => ['Planned', 'Ready'].includes(plan.state);

const canCreateWishlistRequest = (plan) =>
  canExecutePlan(plan) && !plan.wishlistRequestId;

const buildSearchRequest = (plan) => ({
  acquisitionProfile: plan.acquisitionProfile,
  id: uuidv4(),
  searchText: plan.searchText,
});

const buildWishlistRequest = (plan) => ({
  autoDownload: false,
  enabled: true,
  filter: '',
  maxResults: 50,
  searchText: plan.searchText,
});

export const executeAcquisitionPlanSearches = async (
  planIds = [],
  {
    createSearch,
    getItem = getLocalStorageItem,
    maxPlans = 3,
    setItem = setLocalStorageItem,
  } = {},
) => {
  if (typeof createSearch !== 'function') {
    throw new Error('createSearch is required to execute acquisition plans.');
  }

  const selectedIds = new Set(planIds);
  const plans = getAcquisitionPlans(getItem);
  const eligible = plans
    .filter((plan) => selectedIds.size === 0 || selectedIds.has(plan.id))
    .filter(canExecutePlan)
    .slice(0, maxPlans);
  const eligibleIds = new Set(eligible.map((plan) => plan.id));
  const results = [];

  let nextPlans = saveAcquisitionPlans(
    plans.map((plan) =>
      eligibleIds.has(plan.id)
        ? {
            ...plan,
            execution: {
              requestedAt: now(),
              summary:
                'Backend search job requested from approved Discovery Inbox acquisition plan.',
            },
            state: 'Executing',
            updatedAt: now(),
          }
        : plan,
    ),
    setItem,
  );

  for (const plan of eligible) {
    const request = buildSearchRequest(plan);

    try {
      await createSearch(request);
      results.push({
        planId: plan.id,
        searchId: request.id,
        status: 'Queued',
      });
      nextPlans = nextPlans.map((candidate) =>
        candidate.id === plan.id
          ? {
              ...candidate,
              execution: {
                requestedAt: candidate.execution?.requestedAt || now(),
                summary:
                  'Backend search job queued. Download selection still requires normal search-result review.',
              },
              networkImpact:
                'Search job queued through the selected acquisition profile. No download starts until a result is explicitly selected.',
              queuedSearchId: request.id,
              state: 'Queued',
              updatedAt: now(),
            }
          : candidate,
      );
    } catch (error) {
      results.push({
        error: error.message || 'Search request failed.',
        planId: plan.id,
        status: 'Failed',
      });
      nextPlans = nextPlans.map((candidate) =>
        candidate.id === plan.id
          ? {
              ...candidate,
              execution: {
                requestedAt: candidate.execution?.requestedAt || now(),
                summary: error.message || 'Search request failed.',
              },
              state: 'Failed',
              updatedAt: now(),
            }
          : candidate,
      );
    }

    nextPlans = saveAcquisitionPlans(nextPlans, setItem);
  }

  return {
    executed: results.filter((result) => result.status === 'Queued').length,
    failed: results.filter((result) => result.status === 'Failed').length,
    plans: nextPlans,
    results,
    skipped: plans.filter((plan) =>
      (selectedIds.size === 0 || selectedIds.has(plan.id)) &&
      !eligibleIds.has(plan.id)
    ).length,
  };
};

export const executeAcquisitionPlanWishlistRequests = async (
  planIds = [],
  {
    createWishlist,
    getItem = getLocalStorageItem,
    maxPlans = 5,
    setItem = setLocalStorageItem,
  } = {},
) => {
  if (typeof createWishlist !== 'function') {
    throw new Error('createWishlist is required to create acquisition Wishlist requests.');
  }

  const selectedIds = new Set(planIds);
  const plans = getAcquisitionPlans(getItem);
  const eligible = plans
    .filter((plan) => selectedIds.size === 0 || selectedIds.has(plan.id))
    .filter(canCreateWishlistRequest)
    .slice(0, maxPlans);
  const eligibleIds = new Set(eligible.map((plan) => plan.id));
  const results = [];
  let nextPlans = plans;

  for (const plan of eligible) {
    try {
      const wishlist = await createWishlist(buildWishlistRequest(plan));
      const wishlistRequestId = wishlist?.id || uuidv4();

      results.push({
        planId: plan.id,
        status: 'Created',
        wishlistRequestId,
      });
      nextPlans = nextPlans.map((candidate) =>
        candidate.id === plan.id
          ? {
              ...candidate,
              execution: {
                requestedAt: now(),
                summary:
                  'Wishlist request created with auto-download disabled. Downloads still require normal Wishlist review policy.',
              },
              networkImpact:
                'Wishlist request created locally with auto-download disabled. No peer browse or file download starts from this action.',
              updatedAt: now(),
              wishlistRequestId,
            }
          : candidate,
      );
    } catch (error) {
      results.push({
        error: error.message || 'Wishlist request failed.',
        planId: plan.id,
        status: 'Failed',
      });
      nextPlans = nextPlans.map((candidate) =>
        candidate.id === plan.id
          ? {
              ...candidate,
              execution: {
                requestedAt: now(),
                summary: error.message || 'Wishlist request failed.',
              },
              updatedAt: now(),
            }
          : candidate,
      );
    }

    nextPlans = saveAcquisitionPlans(nextPlans, setItem);
  }

  return {
    created: results.filter((result) => result.status === 'Created').length,
    failed: results.filter((result) => result.status === 'Failed').length,
    plans: nextPlans,
    results,
    skipped: plans.filter((plan) =>
      (selectedIds.size === 0 || selectedIds.has(plan.id)) &&
      !eligibleIds.has(plan.id)
    ).length,
  };
};
