const hasValue = (value) =>
  value !== undefined && value !== null && String(value).trim() !== '';

export const buildServarrReadiness = ({
  autoImportCompleted = false,
  enabled = false,
  importPathFrom = '',
  importPathTo = '',
  syncWantedToWishlist = false,
  url = '',
  apiKey = '',
} = {}) => [
  {
    description: 'Lidarr/Radarr/Sonarr-compatible clients need a reachable slskdN URL.',
    id: 'base-url',
    ready: enabled && hasValue(url),
    title: 'Base URL configured',
  },
  {
    description: 'External Servarr apps should use a scoped API key, not an operator session.',
    id: 'api-key',
    ready: hasValue(apiKey),
    title: 'API key configured',
  },
  {
    description: 'Wanted and cutoff-unmet items can be pulled into slskdN Wishlist review.',
    id: 'wanted-pull',
    ready: enabled && syncWantedToWishlist === true,
    title: 'Wanted pull enabled',
  },
  {
    description: 'Completed download import can hand files back to the Servarr app after review.',
    id: 'completed-import',
    ready: enabled && autoImportCompleted === true,
    title: 'Completed import enabled',
  },
  {
    description: 'Remote path mapping prevents container path mismatches during import.',
    id: 'path-map',
    ready: !hasValue(importPathFrom) || hasValue(importPathTo),
    title: 'Remote path mapping sane',
  },
];

export const summarizeServarrReadiness = (checks) => {
  const ready = checks.filter((check) => check.ready).length;

  return {
    ready,
    total: checks.length,
    status: ready === checks.length ? 'Ready' : 'Needs Setup',
  };
};
