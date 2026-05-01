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

export const buildServarrCompatibilityPreview = ({
  apiKey = '',
  autoImportCompleted = false,
  enabled = false,
  importMode = 'copy',
  importPathFrom = '',
  importPathTo = '',
  syncWantedToWishlist = false,
  url = '',
} = {}) => {
  const checks = buildServarrReadiness({
    apiKey,
    autoImportCompleted,
    enabled,
    importPathFrom,
    importPathTo,
    syncWantedToWishlist,
    url,
  });
  const summary = summarizeServarrReadiness(checks);
  const actions = checks
    .filter((check) => !check.ready)
    .map((check) => check.description);

  if (enabled && importMode === 'move' && !autoImportCompleted) {
    actions.push('Enable completed import review before using move-style import handoff.');
  }

  return {
    actions,
    checks,
    importMode,
    summary,
    supportsCompletedImport: enabled && autoImportCompleted === true,
    supportsWantedPull: enabled && syncWantedToWishlist === true,
  };
};

export const formatServarrCompatibilityReport = (preview) => {
  const lines = [
    'slskdN Servarr compatibility review',
    `Status: ${preview.summary.status}`,
    `Checks: ${preview.summary.ready}/${preview.summary.total}`,
    `Wanted pull: ${preview.supportsWantedPull ? 'ready' : 'not ready'}`,
    `Completed import: ${preview.supportsCompletedImport ? 'ready' : 'not ready'}`,
    `Import mode: ${preview.importMode}`,
    '',
    'Checks:',
  ];

  preview.checks.forEach((check) => {
    lines.push(`- ${check.ready ? 'READY' : 'TODO'}: ${check.title}`);
  });

  lines.push('', 'Actions:');
  if (preview.actions.length === 0) {
    lines.push('- none');
  } else {
    preview.actions.forEach((action) => lines.push(`- ${action}`));
  }

  return lines.join('\n');
};
