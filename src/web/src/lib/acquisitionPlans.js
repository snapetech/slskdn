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
    searchText: normalizeText(plan.searchText || plan.title),
    source: plan.source || 'Discovery Inbox',
    sourceId: plan.sourceId || '',
    state: normalizeState(plan.state),
    title: normalizeText(plan.title || plan.searchText || 'Untitled acquisition plan'),
    updatedAt: plan.updatedAt || timestamp,
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
