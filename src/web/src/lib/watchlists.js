// <copyright file="watchlists.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import { getLocalStorageItem, setLocalStorageItem } from './storage';
import {
  acquisitionProfiles,
  defaultAcquisitionProfileId,
  getAcquisitionProfile,
} from './acquisitionProfiles';
import { v4 as uuidv4 } from 'uuid';

export const watchlistStorageKey = 'slskdn.watchlists.items';

const allowedKinds = ['Artist', 'Label', 'Playlist', 'Collection'];
const allowedReleaseTypes = [
  'Album',
  'EP',
  'Single',
  'Compilation',
  'Live',
  'Remix',
  'Deluxe',
];
const allowedCountries = ['Any', 'US', 'GB', 'CA', 'JP', 'DE', 'FR', 'BR', 'AU'];
const allowedFormats = ['Any', 'Digital', 'CD', 'Vinyl', 'Cassette'];
const allowedSchedules = ['Manual only', 'Daily', 'Weekly', 'Monthly'];

const toDropdownOptions = (values) =>
  values.map((value) => ({
    key: value.toLowerCase(),
    text: value,
    value,
  }));

export const watchlistKindOptions = toDropdownOptions(allowedKinds);
export const watchlistReleaseTypeOptions = toDropdownOptions(allowedReleaseTypes);
export const watchlistCountryOptions = toDropdownOptions(allowedCountries);
export const watchlistFormatOptions = toDropdownOptions(allowedFormats);
export const watchlistScheduleOptions = toDropdownOptions(allowedSchedules);
export const watchlistAcquisitionProfileOptions = acquisitionProfiles.map((profile) => ({
  key: profile.id,
  text: profile.label,
  value: profile.id,
}));

const now = () => new Date().toISOString();

const normalizeReleaseTypes = (releaseTypes = []) =>
  releaseTypes.filter((releaseType) => allowedReleaseTypes.includes(releaseType));

const normalizeCountry = (country) =>
  allowedCountries.includes(country) ? country : 'Any';

const normalizeFormat = (format) => (allowedFormats.includes(format) ? format : 'Any');

const normalizeSchedule = (schedule) =>
  allowedSchedules.includes(schedule) ? schedule : 'Manual only';

const normalizeCooldownDays = (cooldownDays) => {
  const parsed = Number(cooldownDays);

  if (!Number.isFinite(parsed)) {
    return 7;
  }

  return Math.min(Math.max(Math.round(parsed), 1), 30);
};

const normalizeWatchlist = (item = {}) => {
  const timestamp = now();

  return {
    acquisitionProfile:
      getAcquisitionProfile(item.acquisitionProfile)?.id ?? defaultAcquisitionProfileId,
    cooldownDays: normalizeCooldownDays(item.cooldownDays),
    country: normalizeCountry(item.country),
    createdAt: item.createdAt || timestamp,
    destination: item.destination || 'Discovery Inbox',
    format: normalizeFormat(item.format),
    id: item.id || uuidv4(),
    kind: allowedKinds.includes(item.kind) ? item.kind : 'Artist',
    lastScannedAt: item.lastScannedAt || '',
    lastScanPreview: item.lastScanPreview || '',
    releaseTypes:
      normalizeReleaseTypes(item.releaseTypes).length > 0
        ? normalizeReleaseTypes(item.releaseTypes)
        : ['Album', 'EP', 'Single'],
    schedule: normalizeSchedule(item.schedule),
    target: item.target || 'Untitled watch',
    updatedAt: item.updatedAt || timestamp,
  };
};

const getWatchlistsWith = (getItem = getLocalStorageItem) => {
  try {
    const parsed = JSON.parse(getItem(watchlistStorageKey, '[]'));
    return Array.isArray(parsed) ? parsed.map(normalizeWatchlist) : [];
  } catch {
    return [];
  }
};

const saveWatchlistsWith = (items, setItem = setLocalStorageItem) => {
  const normalized = items.map(normalizeWatchlist);
  setItem(watchlistStorageKey, JSON.stringify(normalized));
  return normalized;
};

export const getWatchlists = () => getWatchlistsWith();

export const saveWatchlist = (
  item,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
  } = {},
) => {
  const next = normalizeWatchlist(item);
  const existing = getWatchlistsWith(getItem).filter(
    (watch) =>
      !(
        watch.kind === next.kind &&
        watch.target.toLowerCase() === next.target.toLowerCase()
      ),
  );

  return saveWatchlistsWith([next, ...existing], setItem);
};

export const recordWatchlistManualScan = (
  id,
  {
    getItem = getLocalStorageItem,
    setItem = setLocalStorageItem,
    timestamp = now(),
  } = {},
) => {
  const updated = getWatchlistsWith(getItem).map((item) =>
    item.id === id
      ? {
          ...item,
          lastScannedAt: timestamp,
          lastScanPreview:
            'Manual scan preview only; no provider lookup or peer search was started.',
          updatedAt: timestamp,
        }
      : item,
  );

  return saveWatchlistsWith(updated, setItem);
};

export const buildWatchlistSummary = (items = []) =>
  items.reduce(
    (summary, item) => ({
      ...summary,
      [item.kind]: (summary[item.kind] || 0) + 1,
      scheduled:
        item.schedule === 'Manual only' ? summary.scheduled : summary.scheduled + 1,
      total: summary.total + 1,
    }),
    {
      Artist: 0,
      Collection: 0,
      Label: 0,
      Playlist: 0,
      scheduled: 0,
      total: 0,
    },
  );

export const buildWatchlistDiscoverySeed = (item) => ({
  acquisitionProfile: item.acquisitionProfile,
  evidenceKey: `watchlist:${item.kind}:${item.target}`.toLowerCase(),
  networkImpact:
    'Watchlist review seed only; no provider lookup, Soulseek search, peer browse, or download has started.',
  reason: `${item.kind} watchlist target using ${item.releaseTypes.join(', ')} releases, ${item.country} country, and ${item.format} format filters.`,
  searchText: item.target,
  source: 'Watchlist',
  sourceId: item.id,
  title: item.target,
});

export const buildWatchlistSchedulePreview = (item) => {
  const profile = getAcquisitionProfile(item.acquisitionProfile);
  const enabled = item.schedule !== 'Manual only';

  return {
    cooldown: `${item.cooldownDays} day${item.cooldownDays === 1 ? '' : 's'}`,
    enabled,
    label: enabled ? `${item.schedule} schedule visible` : 'Manual scans only',
    networkImpact: enabled
      ? 'Scheduled watchlist scans are enabled in the plan, but this local preview does not execute provider lookups or peer searches.'
      : 'Manual scan previews only; no scheduled provider lookup, peer search, or download is enabled.',
    profileLabel: profile.label,
  };
};
