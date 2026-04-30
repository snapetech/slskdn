import { getLocalStorageItem, setLocalStorageItem } from './storage';
import { buildMetadataMatch } from './metadataMatcher';
import { v4 as uuidv4 } from 'uuid';

export const importStagingStorageKey = 'slskdn.importStaging.items';

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

export const addImportStagingFiles = (
  files,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const items = getImportStagingItems(getItem);
  const fingerprints = new Set(items.map(getFingerprint));
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
