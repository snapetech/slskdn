import { getLocalStorageItem, setLocalStorageItem } from './storage';
import { v4 as uuidv4 } from 'uuid';

export const discoveryInboxStorageKey = 'slskdn.discoveryInbox.items';

export const discoveryInboxStates = [
  'Suggested',
  'Approved',
  'Downloading',
  'Staged',
  'Imported',
  'Rejected',
  'Snoozed',
  'Failed',
];

export const defaultDiscoveryInboxState = 'Suggested';

const now = () => new Date().toISOString();

const normalizeState = (state) =>
  discoveryInboxStates.includes(state) ? state : defaultDiscoveryInboxState;

const normalizeItem = (item) => {
  const timestamp = now();

  return {
    acquisitionProfile: item.acquisitionProfile || 'lossless-exact',
    createdAt: item.createdAt || timestamp,
    evidenceKey: item.evidenceKey || item.title || item.searchText || uuidv4(),
    id: item.id || uuidv4(),
    networkImpact: item.networkImpact || 'Manual review; no network request until approved.',
    reason: item.reason || 'Manual discovery suggestion.',
    searchText: item.searchText || item.title || '',
    source: item.source || 'Manual',
    sourceId: item.sourceId || '',
    state: normalizeState(item.state),
    title: item.title || item.searchText || 'Untitled discovery',
    updatedAt: item.updatedAt || timestamp,
  };
};

export const getDiscoveryInboxItems = (getItem = getLocalStorageItem) => {
  try {
    const parsed = JSON.parse(getItem(discoveryInboxStorageKey, '[]'));
    return Array.isArray(parsed) ? parsed.map(normalizeItem) : [];
  } catch {
    return [];
  }
};

export const saveDiscoveryInboxItems = (
  items,
  setItem = setLocalStorageItem,
) => {
  const normalized = items.map(normalizeItem);
  setItem(discoveryInboxStorageKey, JSON.stringify(normalized));
  return normalized;
};

export const addDiscoveryInboxItem = (
  item,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const items = getDiscoveryInboxItems(getItem);
  const nextItem = normalizeItem(item);
  const duplicate = items.find(
    (existing) =>
      existing.evidenceKey === nextItem.evidenceKey &&
      existing.source === nextItem.source,
  );

  if (duplicate) {
    return duplicate;
  }

  saveDiscoveryInboxItems([nextItem, ...items], setItem);
  return nextItem;
};

export const updateDiscoveryInboxItemState = (
  id,
  state,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const nextState = normalizeState(state);
  const updated = getDiscoveryInboxItems(getItem).map((item) =>
    item.id === id
      ? { ...item, state: nextState, updatedAt: now() }
      : item,
  );

  return saveDiscoveryInboxItems(updated, setItem);
};

export const bulkUpdateDiscoveryInboxItems = (
  ids,
  state,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const idSet = new Set(ids);
  const nextState = normalizeState(state);
  const updated = getDiscoveryInboxItems(getItem).map((item) =>
    idSet.has(item.id)
      ? { ...item, state: nextState, updatedAt: now() }
      : item,
  );

  return saveDiscoveryInboxItems(updated, setItem);
};
