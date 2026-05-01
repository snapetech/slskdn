import { getLocalStorageItem, setLocalStorageItem } from './storage';
import { buildMetadataMatch } from './metadataMatcher';
import { v4 as uuidv4 } from 'uuid';

export const importStagingStorageKey = 'slskdn.importStaging.items';
export const importStagingDenylistStorageKey = 'slskdn.importStaging.denylist';

export const importStagingStates = [
  'Staged',
  'Ready',
  'Imported',
  'Rejected',
  'Failed',
];

const now = () => new Date().toISOString();

const normalizeState = (state) =>
  importStagingStates.includes(state) ? state : 'Staged';

const normalizeItem = (item) => {
  const timestamp = now();

  return {
    createdAt: item.createdAt || timestamp,
    fileName: item.fileName || item.name || 'Unknown file',
    fingerprintVerification: item.fingerprintVerification || null,
    id: item.id || uuidv4(),
    lastModified: item.lastModified || null,
    metadataMatch: item.metadataMatch || null,
    metadataOverride: item.metadataOverride || null,
    reason: item.reason || 'Added from local import staging picker.',
    size: Number.isFinite(item.size) ? item.size : 0,
    state: normalizeState(item.state),
    type: item.type || '',
    updatedAt: item.updatedAt || timestamp,
  };
};

const getFingerprint = (item) =>
  [
    item.fileName,
    item.size,
    item.lastModified || '',
  ].join(':');

const getDenylistKey = (item) =>
  item.fingerprintVerification?.status === 'Verified' &&
  item.fingerprintVerification?.value
    ? `sha256:${item.fingerprintVerification.value}`
    : `file:${getFingerprint(item)}`;

const normalizeDenylistEntry = (entry) => ({
  createdAt: entry.createdAt || now(),
  fileName: entry.fileName || 'Unknown file',
  key: entry.key || '',
  reason: entry.reason || 'Rejected from import staging.',
  sourceState: entry.sourceState || 'Rejected',
});

export const getImportStagingItems = (getItem = getLocalStorageItem) => {
  try {
    const parsed = JSON.parse(getItem(importStagingStorageKey, '[]'));
    return Array.isArray(parsed) ? parsed.map(normalizeItem) : [];
  } catch {
    return [];
  }
};

export const saveImportStagingItems = (
  items,
  setItem = setLocalStorageItem,
) => {
  const normalized = items.map(normalizeItem);
  setItem(importStagingStorageKey, JSON.stringify(normalized));
  return normalized;
};

export const getImportStagingDenylist = (getItem = getLocalStorageItem) => {
  try {
    const parsed = JSON.parse(getItem(importStagingDenylistStorageKey, '[]'));
    return Array.isArray(parsed)
      ? parsed.map(normalizeDenylistEntry).filter((entry) => entry.key)
      : [];
  } catch {
    return [];
  }
};

export const saveImportStagingDenylist = (
  entries,
  setItem = setLocalStorageItem,
) => {
  const normalized = entries
    .map(normalizeDenylistEntry)
    .filter((entry) => entry.key);
  setItem(importStagingDenylistStorageKey, JSON.stringify(normalized));
  return normalized;
};

export const addImportStagingFiles = (
  files,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const items = getImportStagingItems(getItem);
  const fingerprints = new Set(items.map(getFingerprint));
  const denylist = getImportStagingDenylist(getItem);
  const denylistKeys = new Set(denylist.map((entry) => entry.key));
  const added = Array.from(files)
    .map((file) =>
      normalizeItem({
        fileName: file.name,
        fingerprintVerification: file.fingerprintVerification,
        lastModified: file.lastModified,
        size: file.size,
        type: file.type,
      }),
    )
    .filter((item) => {
      const fingerprint = getFingerprint(item);
      if (fingerprints.has(fingerprint)) {
        return false;
      }

      fingerprints.add(fingerprint);
      return true;
    })
    .map((item) => {
      const denylistKey = getDenylistKey(item);
      const denied = denylistKeys.has(denylistKey);

      return denied
        ? {
            ...item,
            reason: 'Blocked by failed-import denylist.',
            state: 'Failed',
          }
        : item;
    });

  return saveImportStagingItems([...added, ...items], setItem);
};

export const updateImportStagingItemState = (
  id,
  state,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const nextState = normalizeState(state);
  const updated = getImportStagingItems(getItem).map((item) =>
    item.id === id
      ? { ...item, state: nextState, updatedAt: now() }
      : item,
  );

  return saveImportStagingItems(updated, setItem);
};

export const addImportStagingItemToDenylist = (
  id,
  reason = 'Rejected from import staging.',
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const item = getImportStagingItems(getItem).find((candidate) => candidate.id === id);
  if (!item) {
    return getImportStagingDenylist(getItem);
  }

  const entry = normalizeDenylistEntry({
    fileName: item.fileName,
    key: getDenylistKey(item),
    reason,
    sourceState: item.state,
  });
  const entries = getImportStagingDenylist(getItem);
  const existing = entries.filter((candidate) => candidate.key !== entry.key);
  return saveImportStagingDenylist([entry, ...existing], setItem);
};

export const removeImportStagingDenylistEntry = (
  key,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const entries = getImportStagingDenylist(getItem).filter(
    (entry) => entry.key !== key,
  );
  return saveImportStagingDenylist(entries, setItem);
};

export const updateImportStagingItemMetadataMatch = (
  id,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const updated = getImportStagingItems(getItem).map((item) =>
    item.id === id
      ? {
          ...item,
          metadataMatch: buildMetadataMatch(item),
          updatedAt: now(),
        }
      : item,
  );

  return saveImportStagingItems(updated, setItem);
};

export const overrideImportStagingItemMetadataMatch = (
  id,
  override,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const updated = getImportStagingItems(getItem).map((item) =>
    item.id === id
      ? {
          ...item,
          metadataMatch: {
            album: override.album || '',
            artist: override.artist || '',
            band: 'Auto',
            confidence: 1,
            evidence: ['Manual metadata override supplied by reviewer.'],
            status: 'Manual Override',
            strongestEvidence: 'Manual metadata override supplied by reviewer.',
            title: override.title || item.fileName,
            trackNumber: override.trackNumber || null,
            warnings: [],
            weakestEvidence: 'No weak evidence.',
          },
          metadataOverride: {
            album: override.album || '',
            artist: override.artist || '',
            title: override.title || item.fileName,
            trackNumber: override.trackNumber || null,
          },
          updatedAt: now(),
        }
      : item,
  );

  return saveImportStagingItems(updated, setItem);
};

export const matchAllImportStagingItems = ({
  getItem = getLocalStorageItem,
  setItem = setLocalStorageItem,
} = {}) => {
  const updated = getImportStagingItems(getItem).map((item) => ({
    ...item,
    metadataMatch: buildMetadataMatch(item),
    updatedAt: now(),
  }));

  return saveImportStagingItems(updated, setItem);
};
